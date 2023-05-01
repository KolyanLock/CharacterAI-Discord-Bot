FROM mcr.microsoft.com/dotnet/sdk:7.0.201-bullseye-slim-amd64 AS build-env
WORKDIR /app

# Копирование csproj-файлов и восстановление зависимостей
COPY *.csproj ./
RUN dotnet restore

# Копирование всего остального и сборка
COPY . ./
RUN dotnet publish -c Release -o out

# Создание образа runtime
FROM mcr.microsoft.com/playwright:bionic
WORKDIR /app

FROM ubuntu:20.04
RUN apt-get update && \
    apt-get install -y wget gnupg ca-certificates && \
    wget -q -O - https://dl-ssl.google.com/linux/linux_signing_key.pub | apt-key add - && \
    echo "deb http://dl.google.com/linux/chrome/deb/ stable main" >> /etc/apt/sources.list.d/google.list && \
    apt-get update && \
    apt-get install -y google-chrome-stable fonts-noto && \
    rm -rf /var/lib/apt/lists/*

# Установка папок для монтирования
RUN mkdir /data && \
    mkdir /data/puppeteer-chrome && \
    mkdir /data/storage

# Копирование бинарных файлов приложения
COPY --from=build-env /app/out ./

# Установка ENV-переменных для puppeteer chrome
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/google-chrome-stable

# Запуск приложения с монтированием папок
ENTRYPOINT ["./CharacterAI_Discord_Bot.dll", "--puppeteer-chrome-path=/data/puppeteer-chrome", "--storage-path=/data/storage"]
VOLUME ["/data/puppeteer-chrome", "/data/storage"]
