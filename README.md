# ArashiDNS.M3
### Server：
```
cd ArashiDNS.M3
dotnet run --key=<YourKeys> --up=8.8.4.4 --urls "http://0.0.0.0:8080"
```
### Client：
```
cd ArashiDNS.M3C
dotnet run http://<ServerIP>:8080/healthz --key=<YourKeys>

# Show Help:
# dotnet run -he
```
