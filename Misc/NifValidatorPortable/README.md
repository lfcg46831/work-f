# NIF Validator Portable

App desktop WPF para validar NIF portugues e identificadores fiscais estrangeiros.

## Abrir a app

Executa:

`C:\Users\pedro\OneDrive\Documentos\New project\NifValidatorPortable\publish\NifValidatorPortable.exe`

## Solucao

Projeto:

`C:\Users\pedro\OneDrive\Documentos\New project\NifValidatorPortable\NifValidatorPortable\NifValidatorPortable.csproj`

Solucao:

`C:\Users\pedro\OneDrive\Documentos\New project\NifValidatorPortable\NifValidatorPortable.slnx`

## Nota sobre o "portable exe"

Foi gerada a pasta `publish` com o `exe` pronto a testar.

Neste ambiente nao foi possivel criar uma publicacao self-contained de ficheiro unico totalmente autonoma, porque os runtime packs necessarios nao estavam disponiveis offline. Se o Windows pedir o runtime .NET Desktop, basta instala-lo ou entao eu posso tentar gerar uma versao self-contained assim que houver acesso aos packs.
