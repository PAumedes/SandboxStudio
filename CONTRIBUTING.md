# Contributing to SandboxStudio

Thank you for your interest in contributing to **SandboxStudio**. We hold engineering craftsmanship, architectural rigor, and design aesthetic to the highest standards.

## Engineering Principles

1. **Test-Driven Development (TDD):** Every feature or bug fix must be accompanied by unit and/or integration tests before merging.
2. **Adversarial Code Review:** All pull requests are reviewed with strict scrutiny for edge cases, performance regressions, and architectural purity.
3. **Pristine Code Style:** Adhere strictly to `.editorconfig`. File-scoped namespaces, nullable reference types, and explicit interface contracts are mandatory.
4. **Documentation:** Public APIs must contain complete, concise XML doc comments.

## Development Workflow

1. Fork the repository and clone locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/SandboxStudio.git
   cd SandboxStudio
   ```
2. Create a focused topic branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. Run test suites locally:
   ```bash
   dotnet test
   ```
4. Commit your changes following Conventional Commits (`feat:`, `fix:`, `refactor:`, `docs:`):
   ```bash
   git commit -m "feat(core): add clipboard direction validation rule"
   ```
5. Push to your branch and open a Pull Request.
