# Specifications

This is a software in node.js + a react ui that is capable of showing a course / presentation made with the rules described in [github-copilot-instruction file](../.github/copilot-instructions.md).

## Specifications

The software will point to a directory where we have a course presentation structured in course, modules (optional), lessons and slides.

The sofware will present the content of each level using the markdown that is contained before the ## Details section, The Details section should never be rendered.

Clicking the screen will navigate into the detail of the root/module/lesson showing the next level. When you are at slide level click will move to the next slide. When all slides are finished the next lesson will be shown and so on.

## Implementation Details

The software will be implemented using Node.js for the backend and React for the frontend. The backend will handle file system operations to read the course presentation structure, while the frontend will be responsible for rendering the content and managing user interactions.