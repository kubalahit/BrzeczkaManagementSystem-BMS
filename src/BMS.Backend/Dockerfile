# Etap 1: Budowanie aplikacji
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Kopiuj pliki projektu i przywróć zależności
COPY *.csproj ./
RUN dotnet restore

# Kopiuj resztę kodu i opublikuj aplikację
COPY . ./
RUN dotnet publish -c Release -o out

# Etap 2: Uruchomienie aplikacji
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "BMS.Backend.dll"]