# API image. Build context is the repo root: the gitignored artifacts/ directory must
# exist locally (mise run builder) — the three planner artifacts are baked into the image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY nuget.config ./
COPY src/Craftiger.Solver/Craftiger.Solver.csproj src/Craftiger.Solver/
COPY src/Craftiger.Api/Craftiger.Api.csproj src/Craftiger.Api/
RUN dotnet restore src/Craftiger.Api
COPY src/Craftiger.Solver/ src/Craftiger.Solver/
COPY src/Craftiger.Api/ src/Craftiger.Api/
RUN dotnet publish src/Craftiger.Api -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
LABEL org.opencontainers.image.source=https://github.com/Kombushi/Craftiger
WORKDIR /app
COPY --from=build /app ./
# ApiOptions:ArtifactsDir defaults to "artifacts" relative to the working directory.
COPY artifacts/ artifacts/
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Craftiger.Api.dll"]
