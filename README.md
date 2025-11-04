# DotNet-Architecture-Examples

**Concise, language-agnostic architecture and design snippets for .NET.**  
A technical lab of focused examples that demonstrate architecture patterns, integration techniques, and performance practices that I used in real-world projects.
Some examples demonstrate well-known, widely adopted patterns, while others showcase custom solutions I’ve designed for specific challenges.

---

## Purpose
This repository is a collection of **small, self-contained examples** that illustrate architectural decisions and implementation patterns in .NET systems. It is intended to show *how* you structure solutions and *why* those choices are made — not to provide full production applications.

---

## Key Topics
- **Architecture & Design**
  - Clean / layered architecture samples
  - CQRS and separation-of-concerns examples
  - Service registration and DI patterns
- **Performance & Reliability**
  - Redis caching and cache invalidation patterns
  - Async processing and throughput considerations
  - Rate limiting and resilient HTTP client patterns

---

## Repo Layout
#### DotNet-Architecture-Examples/
  - Architecture
    - DynamicJsonBuilder.md

  - Performance/
      - RedisCacheSnippet.cs
      - AsyncThroughputDemo.cs
      - RateLimiterConcept.md


Each `.cs` file is a focused snippet; each `.md` explains intent, trade-offs, and integration notes.

---

## Usage
- Read the `.md` files first to understand the problem being solved and its suggested solution.
- Snippets are intentionally minimal — they show patterns and structure rather than full runnable apps.
- Feel free to adapt these ideas into your own projects. If you need a runnable example, I can provide a small demo on request.

---

## Contribution & Attribution
- Contributions and suggestions are welcome (issues / PRs).
- If you reuse code, please keep the author attribution in file headers.

---

## License
This repository is available under the **MIT License** — free to use, modify, and learn from.
