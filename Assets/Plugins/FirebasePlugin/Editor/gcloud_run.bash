#!/usr/bin/env bash
cd ./Assets/Server~
gcloud builds submit --tag gcr.io/coinseast/server
gcloud beta run deploy --image gcr.io/coinseast/server --platform managed
server
europe-west1
y
