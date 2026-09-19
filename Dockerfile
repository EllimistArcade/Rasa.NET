FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app

COPY src /app/src
COPY Rasa.NET.sln /app
COPY Rasa.NET.sln.DotSettings /app

RUN dotnet restore
RUN dotnet build --no-restore
