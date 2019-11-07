#!/usr/bin/env bash
echo "Starting to deploy on server... Please wait... "
cd ./Assets/Server~
$1 builds submit --tag gcr.io/$2/server
$1 beta run deploy --memory=512Mi --image gcr.io/$2/server --platform managed --region europe-west1 --allow-unauthenticated server
exit 0