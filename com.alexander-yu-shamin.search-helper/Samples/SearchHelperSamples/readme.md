# Ignored Files

You can create a `SearchHelperIgnoredFiles` ScriptableObject to specify elements that should be excluded from search results.

The package uses `Regex.IsMatch` to evaluate file paths against your ignore patterns.