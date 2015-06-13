//https://github.com/bertyhell/moviemanager

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing;
using Tmc.BusinessRules.Web.Search;
using Tmc.BusinessRules.Web.SearchTakt;
using Tmc.SystemFrameworks.Common;
using Tmc.SystemFrameworks.Model;
using Tmc.WinUI.Localization;

namespace Tmc.WinUI.Application.Panels.Analyse
{
    class AnalyseWorker : BackgroundWorker
    {
        private readonly TraktSearchClient _searchClient = new TraktSearchClient();

        int totalProgressCounter = 0;
        int passProgressCounter = 0;
        int passes = 1;
        private IList<AnalyseVideo> _analyseVideos;
        public AnalyseWorker()
        {
        }

        public event VideoInfoFoundProgress TotalProgress;

        public void OnTotalProgress(ProgressEventArgs args)
        {
            VideoInfoFoundProgress handler = TotalProgress;
            if (handler != null) handler(this, args);
        }

        public event VideoInfoFoundProgress PassProgress;

        public void OnPassProgress(ProgressEventArgs args)
        {
            VideoInfoFoundProgress handler = PassProgress;
            if (handler != null) handler(this, args);
        }

        internal delegate void VideoInfoFoundProgress(object sender, ProgressEventArgs args);

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            if (e.Argument == null)
            {
                return;
            }
            AnalyseWorkerOptions workerOptions = (AnalyseWorkerOptions)e.Argument;
            _analyseVideos = workerOptions.VideosToAnalyse;

            _searchClient.ClearCache();
            //TODO 070 clear titleguesses and restore tiutleguesses for phase one, but remember to keep user inputted title guesses
            //TODO 005 try to remove some duplicate code from this method
            //TODO 040 give a bonus similarity score to the videoInfo that matches the release year in the filename

            int currentPass = 1;
            if (workerOptions.FullAnalyse) passes = 3;

            ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = -1 };

            //  Filter the videos before we start the threads for each video --> otherwise threads are started unnecessary
            _analyseVideos = GetVideosToAnalyse(_analyseVideos);

            //	Pass 1 (filename / foldername)
            Parallel.ForEach(_analyseVideos,
                options,
                () => currentPass,
                ExecuteFirstPassMain,
                    (pass) => { }
            );

            currentPass++;
            passProgressCounter = 0;
            OnPassProgress(new ProgressEventArgs { MaxNumber = _analyseVideos.Count, ProgressNumber = passProgressCounter });

            //  Filter the videos before we start the threads for each video --> otherwise threads are started unnecessary
            _analyseVideos = GetVideosToAnalyse(_analyseVideos);

            if (workerOptions.FullAnalyse)
            {
                //	Pass 2 (prefixes and suffixes)
                Parallel.ForEach(workerOptions.VideosToAnalyse,
                    options,
                    () => currentPass,
                    ExecuteSecondPass,
                    (pass) => { }
                );

                currentPass++;
                passProgressCounter = 0;
                OnPassProgress(new ProgressEventArgs { MaxNumber = _analyseVideos.Count, ProgressNumber = passProgressCounter });

                //  Filter the videos before we start the threads for each video --> otherwise threads are started unnecessary
                _analyseVideos = GetVideosToAnalyse(_analyseVideos);

                //	Pass 3 (websearch)
                Parallel.ForEach(_analyseVideos,
                    options,
                    () => currentPass,
                    ExecuteThirdPass,
                    (pass) => { }
                    );
            }
        }

        private int ExecuteFirstPassMain(AnalyseVideo analyseVideo, ParallelLoopState loopState, int currentPass)
        {
            //TODO 070 split up in different analysing passes --> only reanalyse videos where no good match was found (or selected by user)

            string fileNameGuess = analyseVideo.GetMainFileNameGuess();
            string folderNameGuess = analyseVideo.GetMainFolderNameGuess();

            FillCandidates(analyseVideo, fileNameGuess, folderNameGuess);

            analyseVideo.HandledTitleGuesses();
            ExecuteAfterPass(currentPass);

            //if (CancellationPending)
            //{
            //	e.Cancel = true;
            //}
            return currentPass;
        }

        private int ExecuteSecondPass(AnalyseVideo analyseVideo, ParallelLoopState loopState, int currentPass)
        {
            //pass2 (remove prefix / suffixes)
            if (analyseVideo.Candidates.Count == 0 || analyseVideo.MatchPercentage < Constants.GREAT_MATCH_FOUND_TRESHOLD)
            {
                string fileNameGuess = analyseVideo.GetMainFileNameGuess();
                string folderNameGuess = analyseVideo.GetMainFolderNameGuess();

                string fileName = Path.GetFileNameWithoutExtension(analyseVideo.Video.Files[0].Path);
                if (fileName != null)
                {
                    List<string> fileNameGuesses = VideoTitleExtractor.GetTitleGuessesFromString(fileName.ToLower(), true);
                    analyseVideo.AddTitleGuesses(fileNameGuesses);
                }
                var directoryName = Path.GetDirectoryName(analyseVideo.Video.Files[0].Path);
                if (directoryName != null)
                {
                    string folderName = directoryName.Split(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Last().ToLower();
                    List<string> folderNameGuesses = VideoTitleExtractor.GetTitleGuessesFromString(folderName, true);
                    analyseVideo.AddTitleGuesses(folderNameGuesses);
                }

                FillCandidates(analyseVideo, fileNameGuess, folderNameGuess);

            }
            analyseVideo.HandledTitleGuesses();
            ExecuteAfterPass(currentPass);

            //if (CancellationPending)
            //{
            //	e.Cancel = true;
            //	return;
            //}
            return currentPass;
        }

        private int ExecuteThirdPass(AnalyseVideo analyseVideo, ParallelLoopState loopState, int currentPass)
        {
            if (analyseVideo.Candidates.Count == 0 || analyseVideo.MatchPercentage < Constants.GREAT_MATCH_FOUND_TRESHOLD)
            {

                analyseVideo.AddTitleGuesses(VideoTitleExtractor.GetTitleGuessesFromPath(analyseVideo.Video.Files[0].Path));
                //TODO 004 optimize this --> also gets done in pass1 --> remember somehow
                UniqueList<string> titleGuesses = analyseVideo.GetTitleGuesses();

                string fileNameGuess = analyseVideo.GetMainFileNameGuess();
                string folderNameGuess = analyseVideo.GetMainFolderNameGuess();

                titleGuesses.Clear();
                foreach (string searchResult in BingSearch.Search(fileNameGuess))
                {
                    analyseVideo.AddTitleGuesses(VideoTitleExtractor.CleanTitle(searchResult));
                }

                if (folderNameGuess != null)
                {
                    foreach (string searchResult in BingSearch.Search(folderNameGuess))
                    {
                        analyseVideo.AddTitleGuesses(VideoTitleExtractor.CleanTitle(searchResult));
                    }
                }

                FillCandidates(analyseVideo, fileNameGuess, folderNameGuess);
            }

            analyseVideo.HandledTitleGuesses();
            ExecuteAfterPass(currentPass);
            //if (CancellationPending)
            //{
            //	e.Cancel = true;
            //	return;
            //}
            return currentPass;
        }

        private void ExecuteAfterPass(int currentPass)
        {
            Interlocked.Add(ref passProgressCounter, 1);
            Interlocked.Add(ref totalProgressCounter, 1);
            OnTotalProgress(new ProgressEventArgs
                            {
                                MaxNumber = _analyseVideos.Count * passes,
                                ProgressNumber = totalProgressCounter,
                                Message = String.Format(Resource.AnalyseWorker_Pass_x_of_y, currentPass, passes)
                            });
            OnPassProgress(new ProgressEventArgs
                            {
                                MaxNumber = _analyseVideos.Count,
                                ProgressNumber = passProgressCounter
                            });
        }

        private void FillCandidates(AnalyseVideo analyseVideo, string fileNameGuess, string folderNameGuess)
        {
            List<string> guesses = new List<string> { fileNameGuess };
            if (folderNameGuess != null)
            {
                guesses.Add(folderNameGuess);
            }
            GetVideoInfos(analyseVideo, guesses);
            analyseVideo.OrderCandidates();

            //TODO 070 add option so user can disable info being changed for this video --> none of the videoInfo's from webservice are correct (maybe video isn't famous enough) --> shouldn't change all movieinfo --> abort analyse for this video

        }

        private void GetVideoInfos(AnalyseVideo analyseVideo, List<string> referenceNames)
        {
            //var candidates = new SortedSet<Video>(new SimilarityComparer()); //sort candidates by their match score with the original filename and foldername

            //TODO 050 analyse name => predict movie or serie => execute suitable block of code blow here

            //if not certainly an episode => alsosearch for movie
            UniqueList<string> uniqueList = analyseVideo.GetTitleGuesses();
            if (analyseVideo.AnalyseType != VideoTypeEnum.Episode)
            {
                int guessIndex = 0;
                //search for videos
                UniqueList<string> titleGuesses = uniqueList;
                while (guessIndex < titleGuesses.Count && analyseVideo.MatchPercentage < Constants.GREAT_MATCH_FOUND_TRESHOLD)
                {
                    string titleGuess = titleGuesses[guessIndex];
                    List<Video> foundCandidates;
                    if (_searchClient.SearchMovie(titleGuess, out foundCandidates))
                    {

                        int candidateIndex = 0;
                        while (candidateIndex < foundCandidates.Count &&
                               analyseVideo.MatchPercentage < Constants.GREAT_MATCH_FOUND_TRESHOLD)
                        {
                            var videoInfo = foundCandidates[candidateIndex];
                            //add pairs of similarity and videoInfo to the list
                            //similarity == max of similarity between to the original guesses for filename and foldername and the videoinfo name from the webservice
                            List<double> similarities = new List<double>();
                            foreach (string referenceName in referenceNames)
                            {
                                similarities.Add(StringSimilarity.GetSimilarity(videoInfo.Name + " " + videoInfo.Release.Year, referenceName));
                                similarities.Add(StringSimilarity.GetSimilarity(videoInfo.Name, referenceName));
                                //TODO 005 give bonus to videoinfo where eg: "men in black" --> original file name contains: mib (first letters of every word)
                            }
                            ;
                            videoInfo.TitleMatchRatio = similarities.Max();
                            FilterAddCandidate(videoInfo, analyseVideo.Candidates);

                            analyseVideo.NotYetAnalysed = false;

                            candidateIndex++;
                        }
                    }
                    analyseVideo.NotYetAnalysed = false;
                    guessIndex++;
                }
            }

            //if no suitable candidate
            //search for tv shows
            if (analyseVideo.MatchPercentage < Constants.GREAT_MATCH_FOUND_TRESHOLD)
            {
                int guessIndex = 0;
                while (guessIndex < uniqueList.Count && analyseVideo.MatchPercentage < Constants.GREAT_MATCH_FOUND_TRESHOLD)
                {
                    string titleGuess = uniqueList[guessIndex];
                    List<Serie> foundCandidates;
                    if (_searchClient.SearchSerie(titleGuess, out foundCandidates))
                    {
                        int candidateIndex = 0;
                        while (candidateIndex < foundCandidates.Count &&
                               analyseVideo.MatchPercentage < Constants.GREAT_MATCH_FOUND_TRESHOLD)
                        {
                            Serie serie = foundCandidates[candidateIndex];
                            Video episode = new Video();
                            if (_searchClient.GetEpisodeDetails(serie.IdTmdb, 1, 1, ref episode))
                            {

                                //add pairs of similarity and videoInfo to the list
                                //similarity == max of similarity between to the original guesses for filename and foldername and the videoinfo name from the webservice

                                List<double> similarities = new List<double>();
                                foreach (string referenceName in referenceNames)
                                {
                                    similarities.Add(StringSimilarity.GetSimilarity(serie.Name, referenceName));
                                    //TODO 005 give bonus to videoinfo where eg: "men in black" --> original file name contains: mib (first letters of every word)
                                }

                                episode.TitleMatchRatio = similarities.Max();
                                FilterAddCandidate(episode, analyseVideo.Candidates);

                                analyseVideo.NotYetAnalysed = false;
                            }
                            candidateIndex++;
                        }
                    }
                    guessIndex++;
                    analyseVideo.NotYetAnalysed = false;
                }
            }
            analyseVideo.NotYetAnalysed = false;
        }

        private void FilterAddCandidate(Video candidate, UniqueList<Video> candidates)
        {
            if (candidate.TitleMatchRatio > Constants.MIN_ACCEPTABLE_TITLE_MATCH_PERCENTAGE && !candidates.Contains(candidate)) //TODO 003 replace by extension method: AddUnique(T)
            {
                candidates.Add(candidate);
            }
        }

        #region helper functions

        private List<AnalyseVideo> GetVideosToAnalyse(IEnumerable<AnalyseVideo> analyseVideos)
        {
            List<AnalyseVideo> retVal = new List<AnalyseVideo>();
            foreach (AnalyseVideo video in analyseVideos)
            {
                if (video.MatchPercentage >= Constants.GREAT_MATCH_FOUND_TRESHOLD)
                {
                    video.Analyse = false;
                }

                if (video.Analyse)
                {
                    retVal.Add(video);
                }
            }
            return retVal;
        }

        #endregion
    }


    public class AnalyseWorkerOptions
    {
        private bool _fullAnalyse;

        public bool FullAnalyse
        {
            get { return _fullAnalyse; }
            set { _fullAnalyse = value; }
        }

        private IList<AnalyseVideo> _videosToAnalyse;

        public IList<AnalyseVideo> VideosToAnalyse
        {
            get { return _videosToAnalyse; }
            set { _videosToAnalyse = value; }
        }

    }

    class SimilarityComparer : IComparer<Video>
    {
        public int Compare(Video x, Video y)
        {
            return (x.IdImdb == y.IdImdb || x.Name == y.Name && x.Release == y.Release) ? 0 : x.TitleMatchRatio < y.TitleMatchRatio ? 1 : -1;
        }
    }
}
