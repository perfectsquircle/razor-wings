# Role & Objective
You are an expert C#, Roslyn, and compiler design engineer. Your task is to build the initial proof-of-concept (PoC) for a Svelte-inspired C# Razor framework. 

This framework operates on a **Compile-Time Transpilation Model**:
1. It takes `.razor` files containing C# component logic and Razor markup.
2. It uses `Microsoft.AspNetCore.Razor.Language` to parse the files.
3. It uses a C# Roslyn Incremental Source Generator (`IIncrementalGenerator`) to inspect C# component state and generate two distinct build outputs:
   - **Server-Side Rendered (SSR) C# class**: Generates initial static HTML strings.
   - **Targeted Vanilla JavaScript**: Surgical, VDOM-free JS DOM manipulation scripts (< 2KB) that bind interactivity and fine-grained state updates on the client.

No WebAssembly or client-side C# runtime is used. The developer writes pure Razor/C#, and the compiler outputs zero-dependency JavaScript.

---

## Technical Stack & Nuget Requirements
- Target Framework: `.NET 10.0`
- `Microsoft.CodeAnalysis.CSharp` (Roslyn Source Generators)
- `Microsoft.AspNetCore.Razor.Language` (Razor Parser AST)

---

## Key Architectural Pipeline

### 1. Parsing Phase
Parse `.razor` documents into an AST. Extract:
- `@code` blocks: Identify component state (fields/properties like `private int count = 0;`) and event methods (`private void Increment() => count++;`).
- Markup bindings: Identify dynamic expressions (e.g., `@count`) and event listener directives (e.g., `@onclick="Increment"` or `@onclick="() => count++"`).

### 2. Analysis & Reactive Graph Phase
- Track state mutations inside event handlers (e.g., unary operations like `count++`, assignments like `count = val`).
- Map each state variable to the DOM node targets where its value is rendered in the Razor markup.

### 3. Code Generation Phase (Build Target)
Emit two outputs per Razor component:
1. **`Component.g.cs`**: A high-performance string builder method for initial SSR execution.
2. **`Component.g.js`**: Pure, imperative JavaScript that attaches DOM event listeners to elements and directly mutates element properties (`element.textContent`, `element.value`, etc.) when corresponding variables change—without a Virtual DOM.

---

## Phase 1 Deliverables & Instructions

Implement the core project structure and a basic end-to-end slice using a simple `Counter.razor` file as the benchmark test case.

### Benchmarked Source Component (`Counter.razor`):
```razor
@code {
    private int count = 0;

    private void Increment()
    {
        count++;
    }
}

<div class="counter">
    <button onclick="@Increment">Increment</button>
    <p>Current count: @count</p>
</div>

```

### Expected Output Target (`Counter.g.js`):

```javascript
export function mountCounter(target) {
  let count = 0;
  const btn = target.querySelector('button');
  const p = target.querySelector('p');

  function update() {
    p.textContent = `Current count: ${count}`;
  }

  btn.addEventListener('click', () => {
    count++;
    update();
  });
}

```

---

## Step-by-Step Execution Plan

1. **Solution Structure Setup:**
* Create a solution with three projects:
* `Framework.Core`: Attributes and core contracts.
* `Framework.Compiler`: Roslyn `IIncrementalGenerator` project referencing `Microsoft.AspNetCore.Razor.Language`.
* `Framework.Sample`: A test ASP.NET Core web project consuming `.razor` files via the compiler.




2. **Parser Integration (`Framework.Compiler`):**
* Configure a custom Razor Engine using `RazorProjectEngine.Create`.
* Implement a C# syntax analyzer that processes the `@code` block syntax tree using Roslyn syntax rewriters to find fields and methods.


3. **JS Emitter Engine:**
* Implement a lightweight JavaScript code generator that translates basic C# unary operators (`count++`), binary assignments (`count += 1`), and string interpolations directly into equivalent ES6 JavaScript statements.


4. **Source Generator Wiring:**
* Wire the compiler to run during compilation using Roslyn's `IIncrementalGenerator` pipeline, emitting the generated C# SSR class and writing the client `.js` asset to the build directory.



Please begin by creating the repository structure, setting up the `.csproj` files with appropriate dependencies, and implementing the Razor parsing utility class.
