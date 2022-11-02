FROM mono:latest

RUN apt-get update -y && apt-get install -y icu-devtools
RUN apt-get install -y mono-complete

COPY ./MsFinder /MsFinder
COPY test.msp /testdata/test.msp
COPY MSFINDER.INI /testdata/MSFINDER.INI