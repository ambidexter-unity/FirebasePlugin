#!/usr/bin/env bash
echo "Starting to deploy locally... Please wait... "
cd ./Assets/Server~
export PATH=/usr/local/bin:/Library/Apple/usr/bin:/Library/Apple/bin:/usr/local/go/bin:$PATH
source ~/.bash_profile
command -v docker >/dev/null 2>&1 || { echo >&2 "Docker not installed. Please install docker."; exit 1; }
docker build . --tag gcr.io/coinseast/server
$1 auth configure-docker
PORT=8080 && docker run \
   -p 8080:${PORT} \
   -e PORT=${PORT} \
   -e K_SERVICE=dev \
   -e K_CONFIGURATION=dev \
   -e K_REVISION=dev-00001 \
   -e GOOGLE_APPLICATION_CREDENTIALS=/tmp/keys/credentials.json \
   -v $GOOGLE_APPLICATION_CREDENTIALS:/tmp/keys/credentials.json:ro \
   gcr.io/coinseast/server