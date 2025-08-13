This is not a software project but a presentation / course structured on markdown file with specific headers. We call this a project

The content is organized in levels:

- Single root slide, the title of the project
- Level 1: modules, an area of the course
- Level 2: lessons, a topic of the area of the course
- Level 3: slides, the effective content of the lesson.

Each of the level is constituited by three different distinct contents. Level 1 can be omitted if the presentation is short and logicall constitued by a single modules, in that situation Level 1 can be omitted.

1. Structured header containing metadata information
2. Representable content: content that is meant to be shown to the class to visually represent the content, it is usually short 
3. Details: are meant to be read by the speaker while presenting as well as to be used for offline view of the presentation.

# Detailed rules

**IMPORTANT**: The division between Representable Content and Details is marked by a two level title `## details`

## Project Header Rules

Each file (root, module, lesson, slide) starts with a **YAML front matter** header.

## YAML Header Schema
```yaml
---
id: "001"                # Unique ID or sortable prefix
level: "root"            # One of: root | module | lesson | slide
title: "Title"           # Display title
parent: null             # ID of the parent (null for root)
---
```

### Ordering and Naming

Order defines sequence within the parent.
Usually use some big number like 10000 so if you need to move things around, you can insert new items without having to rename everything.
Use id as a sortable prefix in filenames (e.g., 0100.module.md) to keep file order aligned with presentation order in the filesystem.
In small projects with a single module, omit the module level and make lessons point directly to the root.

### Folder structures

Each level has its own folder to simplify management, so root course and modules files are in the root folder, then we have a subfolder for each module where all lessons file lives, then another subfolder for each lesson where all slides file lives.

```
course-root/
│
├── 00000.course.md                # Root file: course metadata and intro
│
├── 01000.module.md                # Module 1 metadata and intro
│
├── 01000/                         # Module 1 folder
│   ├── 01000-lesson1.md           # Lesson 1 metadata and intro
│   ├── 02000-lesson2.md           # Lesson 2 metadata and intro
│   └── 01000/             # Lesson 1 folder
│       ├── 01000-slide1.md        # Slide 1 for Lesson 1
│       ├── 02000-slide2.md        # Slide 2 for Lesson 1
│       └── ...                    # More slides
│   └── 02000/             # Lesson 2 folder
│       ├── 01000-slide1.md        # Slide 1 for Lesson 2
│       └── ...                    # More slides
│
├── 02000.module.md                # Module 2 metadata and intro
│
├── 02000/                         # Module 2 folder
│   ├── 01000-lesson1.md           # Lesson 1 metadata and intro
│   └── 01000/             # Lesson 1 folder
│       ├── 01000-slide1.md        # Slide 1 for Lesson 1
│       └── ...                    # More slides
│
└── ...                            # More modules, lessons, and slides
```

## Example, Course about git

This is a possible example of a course about git, where there are included only one module and lesson and a couple of slide as references.

# git.course.md
---
title: "Introduction to Git"
author: "Ricci Gian Maria"
---

# Introduction to Git

> "A deep dive in Git World, starting from internal database structure to the most used everyday command"

## Detail section

This course provides a foundational understanding of Git, a distributed version control system used by developers to manage code changes.  
We will cover basic commands, workflows, and best practices.

---

# 0100.module.md
---
title: "Git internal database"
---

# Git internal database

Git is traditionally viewed as a Source Control System, but it's more accurate to think of it as a content management system that tracks changes to files over time. Its internal database is a key component of this functionality.

## Details
In this module we will examine Git Internal Database structure. The purpose is to understand how Git stores and manages data, enabling us to use it more effectively.

Once you master the git internal structure working with it will be surely simpler.

---

# 0100/0100-blobs.lesson.md
---
title: "Blobs in Git"
---

# Blobs in Git

- Blobs are the basic building blocks of Git's internal storage.
- Each blob represents a file and its contents, stored as a binary object.
- Blobs are identified by their SHA-1 hash, which is computed based on the file's contents.

## Details
The aim of the lesson is to understand the concept of blobs in Git and their role in the internal storage system.

---

# 0100/0100/0100-init.slide.md
---
title: "Create a repository"
---

> Git Init is all you need

## Details

Show that runningn a 

```bash
git init
```

on a folder of the disk a .git folder appears, just navigate the content of the folder to start understanding Git's internal structure.

# 010-010-020-first blob.slide.md

File in folders can be saved in internal database as a simple blob

## Details

The example creat a file and save into internal repo, this is the sequence of the commands

```bash
echo "Hello, Git!" > hello.txt
git hash-object hello.txt
```

Now you can see that a new file is created into `.git/objects/` directory, which contains the blob object representing the file's contents. You can use find command

```bash
find .git/objects/ -type f
```