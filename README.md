# Codex
An extensible platform for indexing and exploring inspired by Source Browser.

[![Build status](https://ci.appveyor.com/api/projects/status/bo3m3aesclsj47wm?svg=true)](https://ci.appveyor.com/project/Ref12/Codex)

# Getting started
* Install [Java JDK](http://www.oracle.com/technetwork/java/javase/downloads/jdk8-downloads-2133151.html)
* Download and unzip [ElasticSearch 5.5.1](https://artifacts.elastic.co/downloads/elasticsearch/elasticsearch-5.5.1.zip)
* Set JAVA_HOME environment variable. Run the following (change the path as needed): `set JAVA_HOME=C:\Program Files\Java\jdk1.8.0_144`.
* Run `.\elasticsearch.bat`
* Open **Codex.sln**
* To index a project,
    * Run **Codex** project, passing in repo's name and path as arguments `-n SampleRepo -p C:\src\codex`
        * `-n _____` is name of your repository
        * `-p _____` points to a location to scan
        * `-es _____` specifies URL to the ElasticSearch server, if it runs on another machine 
        * `-i` (without any args) lets you search through results
* To run the Codex website,
    * Run **Codex.Web** project
 
* To run the Codex website (with [Monaco editor](https://microsoft.github.io/monaco-editor/)),
    * Run **Codex.Web.Monaco** project
