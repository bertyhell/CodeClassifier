# CodeClassifier
You provide a snippet and this c# library will tell you what language it was written in.

##Supported languages:
* ruby
* shell
* c
* c++
* coffee-script
* csharp
* css
* html
* javascript
* objective-c
* python

You can add your own languages by adding a file to the training-set folder. The name of the file has to be the language name. The file has to contain a good representation of the programming language syntax. Look at the other files to get an idea of what that means.

##Technology
Uses .NET 4.0 C#


##Inner workings
The way it calculates the best matching language is by having a number of training files. It reads those, splits the code up in tokens and then generates a tree where every subnode is a possible token to follow the current one.

Then when you want to classify some code, it also tokenizes that code and checks all the match trees that it build in the previous step. The bigger the parts of your code that match with nodes in the match tree, the higher your code scores for a specific language. The language with the highest score will be returned.
