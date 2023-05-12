dotnet publish -c Release -r linux-arm -p:PublishSingleFile=true -o ../publish revpiea.csproj
scp ../publish/revpiea.pdb 172.21.5.106:/home/pi/revpiea
scp ../publish/revpiea 172.21.5.106:/home/pi/revpiea
scp konfiguration.json 172.21.5.106:/home/pi/revpiea