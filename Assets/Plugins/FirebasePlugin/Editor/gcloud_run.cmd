@echo off
echo Starting to deploy... Please wait...
cd .\Assets\Server~
%1 builds submit --tag gcr.io/coinseast/server && %1 beta run deploy --image gcr.io/coinseast/server --platform managed --region europe-west1 --allow-unauthenticated server && echo Deploy complete