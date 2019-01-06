FROM ref12/esimage:5.5.1 AS base
WORKDIR /app
EXPOSE 80

# FROM microsoft/aspnetcore-build:2.0-nanoserver-1709 AS build
# WORKDIR /src
# COPY src/Codex.Web.Mvc/Codex.Web.Mvc.csproj src/Codex.Web.Mvc/
# COPY src/Codex.Sdk/Codex.Sdk.csproj src/Codex.Sdk/
# COPY src/Codex.ElasticSearch.Legacy/Codex.ElasticSearch.Legacy.csproj src/Codex.ElasticSearch.Legacy/
# COPY src/Codex.ElasticSearch/Codex.ElasticSearch.csproj src/Codex.ElasticSearch/
# RUN dotnet restore src/Codex.Web.Mvc/Codex.Web.Mvc.csproj
# COPY . .
# WORKDIR /src/src/Codex.Web.Mvc
# RUN dotnet build Codex.Web.Mvc.csproj -c Release -o /app
# 
# FROM build AS publish
# RUN dotnet publish Codex.Web.Mvc.csproj -c Release -o /app

SHELL ["powershell.exe", "-Command"]

# Create a data volume and map it to the G: drive, allowing Java to call Path.toRealPath() successfully
RUN Set-Variable -Name 'regpath' -Value 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices' ; \
    Set-ItemProperty -path $regpath -Name 'G:' -Value '\??\C:\app' -Type String ;

FROM base AS final
# WORKDIR /app
# COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Codex.Web.Mvc.dll"]
