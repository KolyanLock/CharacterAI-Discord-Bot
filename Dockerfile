FROM mcr.microsoft.com/dotnet/sdk:7.0-focal AS build-env
WORKDIR /app

# Копирование csproj-файлов и восстановление зависимостей
COPY *.csproj ./
RUN dotnet restore

# Копирование всего остального и сборка
COPY . ./
RUN dotnet publish -c Release -o out

# Создание образа runtime
FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-bionic
WORKDIR /app

# Установка зависимостей, необходимых для puppeteer chrome
RUN apt-get update && \
    apt-get install -y wget gnupg ca-certificates && \
    wget -q -O - https://dl-ssl.google.com/linux/linux_signing_key.pub | apt-key add - && \
    echo "deb http://dl.google.com/linux/chrome/deb/ stable main" >> /etc/apt/sources.list.d/google.list && \
    apt-get update && \
    apt-get install -y google-chrome-stable

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
