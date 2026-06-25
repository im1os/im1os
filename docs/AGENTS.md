# AGENTS.md

# IM1 OS AI Engineering Guide

This document defines the engineering standards that every AI assistant (Codex, ChatGPT, OpenAI agents, future AI tools, and human developers) must follow when contributing to IM1 OS.

---

# Project Overview

IM1 OS is a cloud-native, multi-tenant SaaS platform designed to become the operating system for powersports dealerships.

The objective is to provide one integrated platform for:

* Customers
* Vehicles
* Service
* Repair Orders
* Estimates
* Parts
* Inventory
* Purchasing
* Suppliers
* CRM
* Marketing
* Ecommerce
* Accounting
* Reporting
* Employee Management
* AI
* Mobile Applications

The system is intended to replace multiple disconnected dealership systems with one unified platform.

---

# Development Philosophy

Architecture comes before implementation.

Documentation comes before coding.

Business requirements drive architecture.

Architecture drives the database.

The database drives the API.

The API drives the user interface.

AI assists implementation.

AI does not invent architecture.

When uncertain, ask for clarification rather than making assumptions.

---

# Primary Objective

Every decision should support this goal:

> Build the most maintainable, scalable, and understandable dealership platform possible.

Maintainability is more important than cleverness.

Readability is more important than brevity.

Correctness is more important than speed.

---

# Technology Stack

Backend

* .NET 10
* ASP.NET Core

Frontend

* React
* TypeScript

Database

* PostgreSQL

Cache

* Redis

Search

* Meilisearch

Hosting

* Ubuntu
* Contabo VPS

Source Control

* Git
* GitHub

IDE

* Visual Studio Code

---

# Architecture

The application is a Modular Monolith.

NOT microservices.

NOT a traditional layered application.

Modules are independent but execute within one application.

Future extraction into services should remain possible without requiring major rewrites.

---

# Multi-Tenant

Every feature must assume multiple dealerships exist.

Never write code assuming a single dealership.

Tenant isolation is mandatory.

Every tenant owns its own:

* Customers
* Vehicles
* Employees
* Repair Orders
* Inventory
* Financial Data

Global data includes:

* Suppliers
* Product Catalog
* Fitment
* Manufacturers
* Brands

---

# Coding Standards

Prefer explicit code over clever code.

Avoid unnecessary abstraction.

Avoid premature optimization.

Avoid reflection unless absolutely necessary.

Avoid magic strings.

Avoid static state.

Prefer constructor injection.

Prefer immutable objects where practical.

Keep methods small.

Keep classes focused.

---

# Naming

Use meaningful names.

Avoid abbreviations.

Good:

CustomerService

RepairOrder

PurchaseOrder

SupplierCatalog

Bad:

CustSvc

ROMgr

InvProc

---

# Folder Structure

Follow the established repository layout.

Do not invent new top-level folders.

Applications belong in:

apps/

Documentation belongs in:

docs/

Database scripts belong in:

database/

Infrastructure belongs in:

infrastructure/

Reusable libraries belong in:

packages/

Business modules belong in:

modules/

---

# Database

PostgreSQL only.

Every schema change must use migrations.

Never manually modify production tables.

Never drop tables automatically.

Soft delete wherever appropriate.

Every business table should include audit fields.

Use UUIDs where appropriate.

Indexes should be intentional.

Design for millions of records.

---

# Security

Security is never optional.

Never hardcode:

Passwords

API Keys

Connection Strings

Secrets

Use environment variables.

Encrypt sensitive data.

Always validate user input.

Never trust client-side validation.

---

# API

API-first architecture.

REST first.

Version APIs.

Return consistent error responses.

Use DTOs.

Do not expose database entities directly.

---

# User Interface

Consistency is more important than creativity.

Reuse components.

Avoid duplicate UI.

Accessibility matters.

Keyboard navigation matters.

Performance matters.

---

# Business Logic

Business rules belong in the Application layer.

Never embed business logic inside controllers.

Never embed business logic inside React components.

---

# Logging

Log meaningful business events.

Avoid excessive logging.

Never log passwords.

Never log secrets.

Never log payment information.

---

# Git

Small commits.

Meaningful commit messages.

Every commit should leave the solution building successfully.

---

# Documentation

When creating new features:

Update documentation first.

Then implement.

If documentation conflicts with implementation:

Fix the implementation.

Do not silently ignore documentation.

---

# AI Behavior

Before writing code:

Read:

README.md

docs/VISION.md

Relevant architecture documents

Do not invent architecture.

Do not invent database structures.

Do not invent APIs.

If a required design decision is missing:

Stop.

Explain the issue.

Ask for guidance.

---

# Expectations

AI should function as a senior software engineer.

AI should explain important design decisions.

AI should prefer maintainability over novelty.

AI should improve the project while respecting existing architecture.

The objective is not to generate code quickly.

The objective is to build software that will still be understandable and maintainable ten years from now.

---

# Final Rule

Every implementation should answer one question:

**Does this make IM1 OS a better operating system for powersports dealerships?**

If the answer is no, reconsider the design before writing code.
