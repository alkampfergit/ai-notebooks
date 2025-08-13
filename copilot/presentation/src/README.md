# Course Presentation Viewer

A Node.js + React application for viewing structured course presentations with markdown content.

## Features

- Displays course content structured in modules, lessons, and slides
- Separates presentable content from detailed speaker notes
- Click-based navigation through course hierarchy
- Responsive design optimized for presentations
- Supports keyboard navigation (Space, Enter, Arrow keys)
- Automatic progression through slides, lessons, and modules

## Installation

Install all dependencies (backend and frontend):
```bash
npm install
```

This will automatically install both backend and frontend dependencies.

## Usage

### Development Mode

Start both backend and frontend in development mode:
```bash
npm run dev
```

This will start:
- Backend server on http://localhost:3001
- Frontend development server on http://localhost:3000

### Production Mode

Build and start the application:
```bash
npm run build
npm start
```

## Course Structure

The application expects course content to be structured according to the specifications in `claude.md`:

- **Root**: Main course file (`.course.md` or `.bstorm.md`)
- **Modules**: Course sections (`.module.md` files)
- **Lessons**: Topics within modules (`.lesson.md` files in module subdirectories)
- **Slides**: Individual presentation slides (`.slide.md` files in lesson subdirectories)

### File Format

Each markdown file should have:

1. YAML front matter with metadata
2. Presentable content (displayed to audience)
3. Optional `## Details` section (hidden from main view)

Example:
```markdown
---
title: "Introduction to Git"
id: "001"
---

# Introduction to Git

> "A deep dive in Git World"

## Details

This section contains speaker notes and detailed information
that won't be displayed during the presentation.
```

## Navigation

- **Click anywhere** or press **Space/Enter** to advance
- **Arrow keys** also work for navigation
- Automatic progression: Root → Modules → Lessons → Slides
- Progress indicator shows current slide position

## API Endpoints

- `GET /api/course-structure` - Returns the complete course structure
- `GET /api/content/root` - Returns root course content
- `GET /api/content/module/:id` - Returns specific module content
- `GET /api/content/lesson/:moduleId/:lessonId` - Returns specific lesson content
- `GET /api/content/slides/:moduleId/:lessonId` - Returns all slides for a lesson