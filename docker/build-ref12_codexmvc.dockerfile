# ref12/codexmvc
FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build
WORKDIR /bld

COPY . ./
#RUN dotnet restore Codex.sln

# copy everything else and build app
ENV RepackDebugInfo false
WORKDIR /bld/src/Codex.Web.Mvc
RUN dotnet publish -c Release -o /bld/out


FROM mcr.microsoft.com/dotnet/core/aspnet:2.2 AS runtime
WORKDIR /app
COPY --from=build /bld/out ./

EXPOSE 80
ENTRYPOINT ["dotnet", "Codex.Web.Mvc.dll"]