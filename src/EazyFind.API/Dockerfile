FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore first
COPY src/EazyFind.API/EazyFind.API.csproj ./src/EazyFind.API/
RUN dotnet restore src/EazyFind.API/EazyFind.API.csproj

# Copy the rest and build + publish
COPY . .
RUN dotnet build src/EazyFind.API/EazyFind.API.csproj -c Release -o /app/build
RUN dotnet publish src/EazyFind.API/EazyFind.API.csproj -c Release -o /app/publish

# COPY . .
# RUN dotnet publish src/EazyFind.API/EazyFind.API.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EazyFind.API.dll"]
