# Slide Syntax Documentation

This document explains the syntax and options available for creating slides in the presentation system.

## File Structure

Slides should be named with the pattern `*.slide.md` and placed in the appropriate lesson directory structure.

## Frontmatter (YAML Header)

Each slide file begins with a YAML frontmatter block between `---` markers that defines metadata:

```yaml
---
id: "01000-001-010"
level: "slide"
title: "RAG at 10,000 ft"
parent: "01000-001"
background-color: 'orange'
---
```

### Required Fields

- `id`: Unique identifier for the slide
- `level`: Must be "slide"
- `title`: The title of the slide
- `parent`: ID of the parent lesson or module

### Background Options

#### Background Color

Set a solid background color for the slide:

```yaml
background-color: 'orange'
background-color: '#AA00AA'
```

Supports:
- Named colors (e.g., 'orange', 'blue', 'red')
- Hex color codes (e.g., '#AA00AA', '#FF5733')

#### Background Image

Set a background image for the slide:

```yaml
background-image: 'img/search.png'
background-image-transparency: 50
background-image-position: 'center'
```

**Background Image Options:**
- `background-image`: Path to the image file (relative to the slide's directory)
- `background-image-transparency`: Transparency level (0-100, where 0 is fully opaque and 100 is fully transparent)
- `background-image-position`: Image position ('center', 'top', 'bottom', 'left', 'right', etc.)

## Content Structure

The slide content is written in Markdown below the frontmatter:

```markdown
---
# frontmatter here
---

# Main Title

Content goes here using standard Markdown syntax.

- Bullet points
- **Bold text**
- *Italic text*

## Details
This section is for speaker notes and won't be displayed to users.
```

### Content Sections

- **Display Content**: Everything before `## Details` is rendered on the slide
- **Details Section**: Everything after `## Details` is hidden from the presentation view and used for speaker notes or additional context

## Examples

### Simple Slide with Background Color
```yaml
---
id: "example-001"
level: "slide"
title: "Introduction"
parent: "lesson-001"
background-color: '#2E86AB'
---

# Welcome

This is a simple slide with a blue background.
```

### Slide with Background Image
```yaml
---
id: "example-002"
level: "slide"
title: "Architecture Overview"
parent: "lesson-001"
background-image: 'diagrams/architecture.png'
background-image-transparency: 30
background-image-position: 'center'
---

# System Architecture

The background shows our system diagram.

## Details
Explain each component of the architecture in detail here.
```

### Slide with No Background (Default)
```yaml
---
id: "example-003"
level: "slide"
title: "Simple Content"
parent: "lesson-001"
---

# Just Content

This slide uses the default background styling.
```

## Navigation

- **Click anywhere**: Advance to the next slide
- **When all slides in a lesson are complete**: Automatically advance to the next lesson
- **Keyboard shortcuts**: Standard presentation navigation (if implemented)

## Image Assets

Images referenced in `background-image` should be placed relative to the slide file. Common patterns:
- `img/filename.png` - Images in an `img` subdirectory
- `assets/background.jpg` - Images in an `assets` subdirectory
- `../shared/image.png` - Shared images in parent directories

The system serves images through the `/api/images/*` endpoint with security checks to ensure files are within the course directory.