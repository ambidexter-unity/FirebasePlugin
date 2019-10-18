FILE=/usr/local/bin/idea
echo "PATH is $PATH"
if [ -f "$FILE" ]; then
    if [[ ":$PATH:" == *":$FILE:"* ]]; then
      $FILE ./Assets/Server~/src/main/java/io/ambidexter/firebase/Main.java:3
    else
      export PATH=$PATH:$FILE
      $FILE ./Assets/Server~/src/main/java/io/ambidexter/firebase/Main.java:3
    fi
else 
    echo "$FILE does not exist"
    echo "In the IntelliJ IDEA main menu, choose Tools | Create Command-line Launcher. In the Create Launcher Script dialog accept the suggested values '/usr/local/bin/idea'"
    exit 1
fi
