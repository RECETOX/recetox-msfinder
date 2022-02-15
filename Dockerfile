FROM ubuntu:20.04

RUN apt-get update -y && apt-get install -y icu-devtools

COPY ./MsfinderConsoleApp/bin/Release/net5.0/linux-x64/publish /MsfinderConsoleApp
COPY ./MsfinderCommon/Resources /MsfinderConsoleApp/Resources
COPY test.msp /testdata/test.msp
COPY methods.txt /testdata/methods.txt
WORKDIR /MsfinderConsoleApp
