# Используем готовый образ Мicrosoft dotnet sdk 7.0
FROM mcr.microsoft.com/dotnet/sdk:7.0

# Установка HTTPS development certificate
RUN apt-get update \
    && apt-get install -y gnupg \
    && rm -rf /var/lib/apt/lists/* \
    && dotnet dev-certs https --trust
		
# Установка Google Chrome
RUN wget -q -O - https://dl.google.com/linux/linux_signing_key.pub | apt-key add - \
    && echo "deb [arch=amd64] http://dl.google.com/linux/chrome/deb/ stable main" >> /etc/apt/sources.list.d/google-chrome.list \
    && apt-get update \
    && apt-get install -y google-chrome-stable \
    && rm -rf /var/lib/apt/lists/*

# Установка рабочей директории
WORKDIR /app

# Копируем готовую сборку приложения и дополнительные файлы
COPY . /app/

# Изменяем права доступа для папки /app и ее содержимого
RUN chmod -R 777 /app

# Если папка /app/storage не существует или пуста, тогда копируем папку /app/temp/storage в /app/storage 
RUN if test ! -d ./storage || test -z "$(ls -A ./storage)"; then cp -r ./temp/storage ./; fi

# Удаляем временную папку
RUN rm -rf ./temp

# Устанавливаем точку входа на запуск приложения
ENTRYPOINT ["./CharacterAI_Discord_Bot"]