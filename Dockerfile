FROM mono:latest

RUN apt-get update -y && apt-get install -y icu-devtools
RUN apt-get install -y mono-complete
RUN apt-get install -y wget

COPY ./ /MsFinder

WORKDIR /MsFinder
RUN wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe
RUN mono nuget.exe restore
RUN msbuild MsfinderConsoleApp/