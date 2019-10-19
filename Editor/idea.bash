#!/usr/bin/env bash
FILE=/usr/local/bin/idea
if [ -f "$FILE" ]; then
    if [[ ":$PATH:" == *":$FILE:"* ]]; then
      $FILE ./Assets/Server~
    else
      export PATH=$PATH:$FILE
      $FILE ./Assets/Server~
    fi
else 
    echo "$FILE does not exist"
    echo "In the IntelliJ IDEA main menu, choose Tools | Create Command-line Launcher. In the Create Launcher Script dialog accept the suggested values '/usr/local/bin/idea'"
    exit 1
fi
