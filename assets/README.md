# Значок приложения

Оригинальный знак «кавычки и стрелка»: белые кавычки и голубая стрелка на синем фоне. Лицензия MIT, как у кода проекта.

`ThirdPerson.svg` — векторный исходник. `ThirdPerson.ico` содержит PNG-кадры 16, 20, 24, 32, 40, 48, 64, 128 и 256 пикселей. `Build-Icon.cs` рисует каждый размер отдельно средствами System.Drawing и собирает ICO.

Пересоздать ICO из корня проекта (Windows, компилятор .NET Framework):

```powershell
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$builder = Join-Path $env:TEMP 'ThirdPerson-Build-Icon.exe'
& $compiler /nologo /target:exe /reference:System.Drawing.dll "/out:$builder" .\assets\Build-Icon.cs
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки генератора' }
& $builder .\assets\ThirdPerson.ico (Join-Path $env:TEMP 'ThirdPerson-icon.png')
if ($LASTEXITCODE -ne 0) { throw 'Ошибка генерации' }
```

Значок встраивается в ресурсы Windows EXE и в ресурс формы, а также используется Inno Setup. Отдельного ICO в установленной папке не требуется.
