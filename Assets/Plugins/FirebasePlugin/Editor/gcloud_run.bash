#!/usr/bin/env bash
echo "Starting to deploy on server... Please wait... "
cd ./Assets/Server~
$1 builds submit --tag gcr.io/coinseast/server
$1 beta run deploy --image gcr.io/coinseast/server --platform managed --region europe-west1 --allow-unauthenticated server
exit 0