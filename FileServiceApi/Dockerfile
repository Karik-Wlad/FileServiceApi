# Используем официальный образ .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копируем файл проекта и восстанавливаем зависимости
COPY *.csproj ./
RUN dotnet restore

# Копируем все остальные файлы и собираем проект
COPY . ./
RUN dotnet publish -c Release -o out

# Используем официальный образ .NET ASP.NET для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Запускаем наше приложение
ENTRYPOINT ["dotnet", "FileServiceApi.dll"]