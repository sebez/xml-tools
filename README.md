# xml-tools

Outils de manipulation du XML.

## Transformation TRX vers playlist

### Contexte

Ma build CI exécute mes tests unitaires C# et produit un fichier XML au format TRX étant le résultat des tests.

Sur mon Visual Studio 2019, je voudrais avoir une playlist contenant uniquement les tests en erreur.

## Solution

Le transformeur lit le fichier TRX et produit un fichier XML de playlistde tests unitaires pour Visual Studio.
