FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /jobs

# Copy csproj and restore first
COPY jobs/EazyFind.Jobs/EazyFind.Jobs.csproj ./jobs/EazyFind.Jobs/
RUN dotnet restore jobs/EazyFind.Jobs/EazyFind.Jobs.csproj

# Copy the rest and build + publish
COPY . .
RUN dotnet build jobs/EazyFind.Jobs/EazyFind.Jobs.csproj -c Release -o /app/build
RUN dotnet publish jobs/EazyFind.Jobs/EazyFind.Jobs.csproj -c Release -o /app/publish

# COPY . .
# RUN dotnet publish jobs/EazyFind.Jobs/EazyFind.Jobs.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EazyFind.Jobs.dll"]
