FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
ARG GIT_COMMIT=unknown

# Install clang and other required dependencies
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        clang \
        zlib1g-dev \
        cmake \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY ["Repliqate/Repliqate.csproj", "Repliqate/"]
RUN dotnet restore "Repliqate/Repliqate.csproj"
COPY . .
WORKDIR "/src/Repliqate"
RUN dotnet build "./Repliqate.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:Version=$VERSION /p:SourceRevisionId=$GIT_COMMIT


FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
ARG GIT_COMMIT=unknown
RUN dotnet publish "./Repliqate.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=$VERSION /p:SourceRevisionId=$GIT_COMMIT

FROM mcr.microsoft.com/dotnet/runtime:9.0

# Install tzdata
RUN apt-get update && \
    apt-get install -y tzdata && \
    rm -rf /var/lib/apt/lists/*

# Allow setting timezone via ENV
ENV TZ=UTC
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Repliqate.dll"]
