# Geren.Server.Exporter

Exporter сканирует проект и строит JSON-спецификацию по Minimal API (`MapGet/MapPost/...`).

## Важно про `MapGroup`

Префиксы групп учитываются **только** когда они задаются **константной строкой** (compile-time constant):

```csharp
app.MapGroup("stat").MapPost("setItems/{tourId:int}", ...);
const string Prefix = "stat";
app.MapGroup(Prefix).MapPost("setItems/{tourId:int}", ...);
```

Не используйте `MapGroup(Func<string>)`, `MapGroup(MethodBase)`, собственные wrapper-extension’ы с reflection и любую runtime-логику для построения префикса — такие префиксы exporter не обязан (и обычно не сможет) определить.

## Warnings

Exporter пишет предупреждения в stderr. При необходимости их можно включить в JSON:

```powershell
Geren.Server.Exporter --project .\MyServer.csproj --output-dir .\artifacts --IncludeWarningsInJson true
```
