#!/usr/bin/env bash
echo "Stopping previously running containers... Please wait... "
export PATH=/usr/local/bin:/Library/Apple/usr/bin:/Library/Apple/bin:/usr/local/go/bin:$PATH
source ~/.bash_profile
command -v docker >/dev/null 2>&1 || { echo >&2 "Docker not installed. Please install docker."; exit 1; }
docker stop $(docker ps -a -q)