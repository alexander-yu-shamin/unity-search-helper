# Filter Rules

Create a `SearchHelperFilterRules` ScriptableObject to configure search filters — specify exactly which elements to include or exclude.

A `SearchHelperFilterRules` ScriptableObject can contain multiple rules, each defined by:
- `Mode`: Include or Exclude
- `Target`: Path, Name, or Type
- `Pattern`: Evaluated using Regex.IsMatch