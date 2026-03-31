# AI Assistant Instructions for Common C# Code Conventions

These instructions define preferred C# coding conventions, style rules, and implementation guidance for code generated or modified by an AI assistant in this repository.

Follow these rules unless the repository already enforces a stronger or more specific standard.

## 1. Goals of these conventions

These conventions are intended to optimize for:

- Correctness
- Teaching value and clarity
- Consistency
- Adoption of modern C# features

Code should be easy to read, maintain, extend, and safely copy into real applications.

---

## 2. Tools and analyzers

Use tooling to enforce conventions whenever possible.

### Preferred practices

- Respect repository `.editorconfig` settings.
- Enable code analysis rules where supported.
- Treat analyzer warnings seriously.
- Prefer fixes that improve readability and correctness, not just compilation.

### Diagnostic guidance

If creating custom analyzers or rule documentation, choose clear and appropriate diagnostic IDs.

---

## 3. General language guidelines

When writing C#:

- Use modern C# language features whenever practical.
- Avoid outdated constructs when a modern alternative is clearer.
- Catch only exceptions you can properly handle.
- Avoid catching `System.Exception` unless there is a strong reason and appropriate filtering.
- Prefer specific exception types.
- Use LINQ for collection manipulation when it improves readability.
- Use `async` and `await` for I/O-bound work.
- Be mindful of deadlocks and use `ConfigureAwait` when appropriate to the application type.
- Use language keywords for built-in types:
  - `string` instead of `System.String`
  - `int` instead of `System.Int32`
  - `nint` and `nuint` where appropriate
- Prefer `int` over unsigned integer types unless the domain specifically requires unsigned behavior.
- Use `var` only when the type is obvious from the right-hand side, except where these instructions explicitly prefer implicit typing.
- Write code for clarity and simplicity.
- Avoid convoluted logic.

---

## 4. Strings

### Prefer string interpolation

Use interpolation for short string composition.

```csharp
string displayName = $"{person.LastName}, {person.FirstName}";
```

### Use `StringBuilder` in loops or large text assembly

```csharp
var manyPhrases = new StringBuilder();
for (var i = 0; i < 10000; i++)
{
    manyPhrases.Append(phrase);
}
```

### Prefer raw string literals

Use raw string literals instead of heavy escaping or verbatim strings when they improve readability.

```csharp
var message = """
    This is a long message.
    It can contain \n and \t literally.
    """;
```

### Prefer expression-based interpolation over positional formatting

```csharp
Console.WriteLine($"{student.Last} Score: {student.Score}");
```

---

## 5. Constructors and initialization

### Primary constructor casing

- Use **PascalCase** for primary constructor parameters on record types.
- Use **camelCase** for primary constructor parameters on class and struct types.

### Prefer required properties when appropriate

Use `required` properties instead of large constructors when the goal is to enforce initialization of important data.

```csharp
public class LabelledContainer<T>(string label)
{
    public string Label { get; } = label;

    public required T Contents
    {
        get;
        init;
    }
}
```

---

## 6. Arrays and collections

### Prefer collection expressions

Use collection expressions to initialize collection types when supported.

```csharp
string[] vowels = ["a", "e", "i", "o", "u"];
```

---

## 7. Delegates

### Prefer `Func<>` and `Action<>`

Use `Func<>` and `Action<>` instead of defining custom delegate types unless a named delegate adds real value.

```csharp
Action<string> log = x => Console.WriteLine($"x is: {x}");
Func<int, int, int> add = (x, y) => x + y;
```

### If using delegate types, prefer concise instantiation

```csharp
public delegate void Del(string message);

public static void DelMethod(string str)
{
    Console.WriteLine($"DelMethod argument: {str}");
}

Del example = DelMethod;
example("Hey");
```

Avoid verbose delegate construction unless needed for explanation or compatibility.

---

## 8. Exception handling and disposal

### Use `try-catch` for exception handling

Catch specific exceptions you can handle meaningfully.

```csharp
try
{
    return ComputeValue();
}
catch (ArithmeticException ex)
{
    Console.WriteLine($"Arithmetic problem: {ex}");
    throw;
}
```

### Prefer `using` over `try-finally` for disposal

If the only `finally` behavior is disposal, use `using`.

Prefer the modern form:

```csharp
using Font normalStyle = new Font("Arial", 10.0f);
byte charset = normalStyle.GdiCharSet;
```

---

## 9. Boolean operators

Use short-circuiting operators in conditional logic:

- `&&` instead of `&`
- `||` instead of `|`

```csharp
if ((divisor != 0) && (dividend / divisor) is var result)
{
    Console.WriteLine($"Quotient: {result}");
}
```

Use the non-short-circuiting forms only when you explicitly need both operands evaluated.

---

## 10. Object creation

### Prefer concise `new` syntax

Use target-typed `new()` or `var` with `new Type(...)` when the type is obvious.

```csharp
var firstExample = new ExampleClass();
ExampleClass secondExample = new();
```

### Prefer object initializers

```csharp
var machine = new ExampleClass
{
    Name = "Desktop",
    ID = 37414,
    Location = "Redmond",
    Age = 2.3
};
```

---

## 11. Event handling

Use lambda expressions for event handlers that do not need later removal.

```csharp
this.Click += (s, e) =>
{
    MessageBox.Show(((MouseEventArgs)e).Location.ToString());
};
```

Use named handlers when they need to be detached, reused, or independently tested.

---

## 12. Static members

Always access static members using the declaring type name.

```csharp
ClassName.StaticMember
```

Do not qualify a base-class static member through a derived type, even if it compiles.

---

## 13. LINQ guidelines

### Naming

Use meaningful query variable names.

```csharp
var seattleCustomers =
    from customer in customers
    where customer.City == "Seattle"
    select customer.Name;
```

### Anonymous types

Use PascalCase aliases for anonymous type properties.

```csharp
select new { Customer = customer, Distributor = distributor };
```

Rename properties to avoid ambiguity.

```csharp
select new
{
    CustomerName = customer.Name,
    DistributorName = distributor.Name
};
```

### Typing

Prefer implicit typing for LINQ queries and range variables.

```csharp
var names =
    from customer in customers
    where customer.City == "Seattle"
    select customer.Name;
```

### Layout

- Align query clauses beneath the `from` clause.
- Put `where` clauses before later query clauses when possible.
- Use multiple `from` clauses to access nested collections when that is clearer than a join.

---

## 14. Implicitly typed local variables

### Use `var` when the type is obvious

Good examples:

```csharp
var message = "This is clearly a string.";
var currentTemperature = 27;
var stream = new MemoryStream();
var count = (int)someValue;
```

### Do not use `var` when the type is not obvious

Avoid:

```csharp
var result = ExampleClass.ResultSoFar();
var numberOfIterations = Convert.ToInt32(Console.ReadLine());
```

Prefer:

```csharp
int result = ExampleClass.ResultSoFar();
int numberOfIterations = Convert.ToInt32(Console.ReadLine());
```

### Do not encode types in variable names

Avoid names like:

```csharp
var inputInt = Console.ReadLine();
```

Prefer names that communicate meaning, not type.

### `dynamic` vs `var`

Do not use `var` as a substitute for `dynamic`.
Use `dynamic` only when run-time binding is the intended behavior.

### Loop variables

- Use implicit typing for `for` loop counters.
- Use explicit typing for `foreach` loop variables when the element type is not immediately obvious.

```csharp
for (var i = 0; i < 10; i++)
{
}

foreach (char ch in laugh)
{
}
```

### LINQ exception

Use implicit typing for LINQ result sequences, even when the type is not obvious, because the resulting type is often anonymous or too noisy to write explicitly.

---

## 15. Namespaces and using directives

### Prefer file-scoped namespaces

Use file-scoped namespaces when a file declares a single namespace.

```csharp
namespace MySampleCode;
```

### Place `using` directives outside the namespace

Preferred:

```csharp
using Azure;

namespace CoolStuff.AwesomeFeature;
```

Avoid placing `using` directives inside the namespace body because it makes name resolution more context-sensitive and potentially brittle.

If needed, use `global::` explicitly, but prefer outer-scope `using` directives first.

---

## 16. Style guidelines

### Indentation and spacing

- Use four spaces for indentation.
- Do not use tabs.
- Keep alignment consistent.

### Line length

- Prefer lines of about 65 characters or less for documentation-heavy code samples.
- Break long statements for readability.

### Braces

Use Allman style braces.

```csharp
if (condition)
{
    DoWork();
}
```

### Binary operators

When a line break is necessary, place the break before the binary operator.

---

## 17. Comment style

### Use single-line comments for short explanations

```csharp
// The following declaration creates a query.
```

### Avoid block comments for long explanations

Prefer moving long explanations into surrounding documentation or prose instead of embedding them in code.

### XML comments

Use XML documentation comments for:

- public methods
- public classes
- public fields
- all public members

### Comment formatting rules

- Place comments on their own line, not at the end of a code line.
- Start with an uppercase letter.
- End with a period.
- Put one space after `//`.

---

## 18. Layout conventions

Follow these layout rules:

- One statement per line.
- One declaration per line.
- Indent continuation lines by one indentation level when not automatically aligned.
- Add at least one blank line between methods and properties.
- Use parentheses to make expression grouping clear.

Example:

```csharp
if ((startX > endX) && (startX > previousX))
{
    // Take appropriate action.
}
```

Exceptions are allowed when the sample is specifically teaching operator precedence.

---

## 19. Security

Follow secure coding practices.

At minimum:

- validate untrusted input
- avoid broad exception suppression
- avoid insecure defaults
- use safe APIs where available
- avoid leaking secrets in logs or exceptions
- follow repository or platform secure coding guidance when present

If there is a conflict between style and security, prefer security.

---

## 20. Assistant rules for generated C# code

When generating or modifying C# code in this repository, always follow these rules:

1. Prefer modern C# syntax when it improves readability.
2. Use language keywords like `string` and `int`.
3. Use `var` only when the type is obvious, except for LINQ result sequences and simple `for` counters.
4. Prefer interpolation over concatenation for short strings.
5. Prefer `StringBuilder` for repeated string appends.
6. Prefer `using` declarations over disposal-oriented `try-finally`.
7. Catch only exceptions that can be handled meaningfully.
8. Use `&&` and `||` for conditional logic.
9. Use concise object creation syntax and object initializers when appropriate.
10. Use the declaring type to access static members.
11. Use file-scoped namespaces by default.
12. Place `using` directives outside the namespace.
13. Use Allman braces and four-space indentation.
14. Keep comments short, clear, and correctly formatted.
15. Prefer clarity over cleverness.

If existing repository conventions differ, prefer the repository’s local conventions unless this file is intended to become the new enforced standard.
