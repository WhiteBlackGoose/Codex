This project/folder defines containers.

-- Externally defined containers
microsoft/dotnet:2.1-aspnetcore-runtime-nanoserver-sac2016 
    ASP.NET Runtime

ref12/esimage-5.5.1:ES_5.5.1 extends microsoft/dotnet:2.1-aspnetcore-runtime-nanoserver-sac2016
    \ext\es:  ElasticSearch 5.5.1
    \ext\jdk: JDK 1.8.0.111


-- Locally defined containers
aspnet_es_5.5.1 extends ref12/esimage-5.5.1:ES_5.5.1
    \app:     Codex.Web.Mvc