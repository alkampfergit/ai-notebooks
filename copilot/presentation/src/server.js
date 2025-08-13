const express = require('express');
const cors = require('cors');
const fs = require('fs-extra');
const path = require('path');
const matter = require('gray-matter');
const { marked } = require('marked');

const app = express();
const PORT = process.env.PORT || 3001;

app.use(cors());
app.use(express.json());

function findCourseRoot() {
  let currentDir = process.cwd();
  const searchedDirs = [];
  
  console.log(`\n🔍 Starting course file search from: ${currentDir}`);
  
  // Search current and parent directories for .course files
  while (currentDir !== path.parse(currentDir).root) {
    try {
      const files = fs.readdirSync(currentDir);
      const mdFiles = files.filter(f => f.endsWith('.md'));
      searchedDirs.push({ dir: currentDir, files: mdFiles });
      
      const courseFile = files.find(file => file.includes('.course.') && file.endsWith('.md'));
      
      if (courseFile) {
        console.log(`✅ Found course file: ${courseFile} in ${currentDir}`);
        return currentDir;
      }
    } catch (error) {
      console.log(`❌ Cannot read directory: ${currentDir} - ${error.message}`);
    }
    
    currentDir = path.dirname(currentDir);
  }
  
  // No course file found - dump search results
  console.log('\n❌ No .course.md file found!');
  console.log('\n📁 Searched directories and found .md files:');
  searchedDirs.forEach(({ dir, files }) => {
    console.log(`  ${dir}:`);
    if (files.length === 0) {
      console.log(`    (no .md files)`);
    } else {
      files.forEach(file => console.log(`    - ${file}`));
    }
  });
  
  console.log(`\n⚠️  Using current directory as fallback: ${process.cwd()}`);
  return process.cwd();
}

const COURSE_ROOT = findCourseRoot();

function parseMarkdownFile(filePath) {
  try {
    const content = fs.readFileSync(filePath, 'utf8');
    const parsed = matter(content);
    
    const sections = parsed.content.split(/^## Details?$/im);
    const displayContent = sections[0].trim();
    const details = sections[1] ? sections[1].trim() : '';
    
    return {
      metadata: parsed.data,
      displayContent: marked(displayContent),
      details: details ? marked(details) : null,
      filePath: path.relative(COURSE_ROOT, filePath),
      fileDir: path.relative(COURSE_ROOT, path.dirname(filePath))
    };
  } catch (error) {
    console.error(`Error parsing file ${filePath}:`, error);
    return null;
  }
}

function getCourseStructure() {
  const structure = {
    root: null,
    modules: [],
    lessons: {},
    slides: {}
  };

  console.log(`\n📖 Reading course structure from: ${COURSE_ROOT}`);

  try {
    const files = fs.readdirSync(COURSE_ROOT);
    console.log(`Found ${files.length} files in course root`);
    
    const mdFiles = files.filter(f => f.endsWith('.md'));
    console.log(`Found ${mdFiles.length} markdown files:`, mdFiles);
    
    for (const file of files) {
      if (!file.endsWith('.md')) continue;
      
      const filePath = path.join(COURSE_ROOT, file);
      const parsed = parseMarkdownFile(filePath);
      
      if (!parsed) continue;
      
      if (file.includes('.course.')) {
        structure.root = { ...parsed, filename: file };
        console.log(`✅ Set root content from: ${file}`);
      } else if (file.includes('.module.')) {
        structure.modules.push({ ...parsed, filename: file });
        console.log(`📚 Added module: ${file}`);
      }
    }
    
    structure.modules.sort((a, b) => {
      const aId = a.metadata.id || a.filename;
      const bId = b.metadata.id || b.filename;
      return aId.localeCompare(bId);
    });

    for (const module of structure.modules) {
      const moduleId = module.metadata.id || module.filename.replace('.module.md', '');
      const modulePath = path.join(COURSE_ROOT, moduleId);
      
      if (fs.existsSync(modulePath) && fs.statSync(modulePath).isDirectory()) {
        structure.lessons[moduleId] = [];
        
        const lessonFiles = fs.readdirSync(modulePath).filter(f => f.endsWith('.lesson.md'));
        
        for (const lessonFile of lessonFiles) {
          const lessonPath = path.join(modulePath, lessonFile);
          const parsed = parseMarkdownFile(lessonPath);
          
          if (parsed) {
            const lessonId = lessonFile.replace('.lesson.md', '');
            structure.lessons[moduleId].push({ ...parsed, filename: lessonFile, id: lessonId });
            console.log(`📖 Added lesson: ${lessonId} from ${lessonFile}`);
            
            // Extract just the numeric part from lesson filename (e.g., "01000" from "01000-lesson1.md")
            const numericId = lessonFile.split('-')[0];
            const slidesPath = path.join(modulePath, numericId);
            console.log(`🔍 Looking for slides in: ${slidesPath} (extracted numeric ID: ${numericId})`);
            
            if (fs.existsSync(slidesPath) && fs.statSync(slidesPath).isDirectory()) {
              structure.slides[lessonId] = [];
              
              const slideFiles = fs.readdirSync(slidesPath).filter(f => f.endsWith('.slide.md'));
              console.log(`📄 Found ${slideFiles.length} slide files:`, slideFiles);
              
              for (const slideFile of slideFiles) {
                const slideFilePath = path.join(slidesPath, slideFile);
                const parsed = parseMarkdownFile(slideFilePath);
                
                if (parsed) {
                  structure.slides[lessonId].push({ ...parsed, filename: slideFile });
                  console.log(`✅ Added slide: ${slideFile}`);
                }
              }
              
              structure.slides[lessonId].sort((a, b) => a.filename.localeCompare(b.filename));
            } else {
              console.log(`❌ Slides directory not found: ${slidesPath}`);
              // Check what exists in the module directory
              try {
                const moduleContents = fs.readdirSync(modulePath);
                console.log(`📁 Module directory contents:`, moduleContents);
              } catch (e) {
                console.log(`❌ Cannot read module directory: ${e.message}`);
              }
            }
          }
        }
        
        structure.lessons[moduleId].sort((a, b) => a.filename.localeCompare(b.filename));
      }
    }
    
    // Final validation
    if (!structure.root) {
      console.log('\n❌ ERROR: No root content found!');
      console.log('   Expected: A file containing .course. in its name');
      console.log('   Found files:', mdFiles);
    } else {
      console.log(`\n✅ Course structure loaded successfully`);
      console.log(`   Root: ${structure.root.filename}`);
      console.log(`   Modules: ${structure.modules.length}`);
    }
    
    return structure;
  } catch (error) {
    console.error('Error reading course structure:', error);
    return structure;
  }
}

app.get('/api/course-structure', (req, res) => {
  const structure = getCourseStructure();
  res.json(structure);
});

app.get('/api/content/:type/:id?/:lessonId?', (req, res) => {
  const { type, id, lessonId } = req.params;
  const structure = getCourseStructure();
  
  try {
    switch (type) {
      case 'root':
        res.json(structure.root);
        break;
      case 'module':
        const module = structure.modules.find(m => 
          (m.metadata.id || m.filename.replace('.module.md', '')) === id
        );
        res.json(module);
        break;
      case 'lesson':
        const lessons = structure.lessons[id] || [];
        const lesson = lessons.find(l => l.id === lessonId);
        res.json(lesson);
        break;
      case 'slides':
        const slides = structure.slides[lessonId] || [];
        res.json(slides);
        break;
      default:
        res.status(404).json({ error: 'Content type not found' });
    }
  } catch (error) {
    console.error('Error fetching content:', error);
    res.status(500).json({ error: 'Internal server error' });
  }
});

app.get('/api/images/*', (req, res) => {
  const imagePath = req.params[0];
  const fullPath = path.join(COURSE_ROOT, imagePath);
  
  // Security check: ensure the path is within COURSE_ROOT
  if (!fullPath.startsWith(COURSE_ROOT)) {
    return res.status(403).json({ error: 'Access denied' });
  }
  
  if (fs.existsSync(fullPath)) {
    res.sendFile(fullPath);
  } else {
    res.status(404).json({ error: 'Image not found' });
  }
});

if (process.env.NODE_ENV === 'production') {
  app.use(express.static(path.join(__dirname, 'client/build')));
  
  app.get('*', (req, res) => {
    res.sendFile(path.join(__dirname, 'client/build/index.html'));
  });
}

app.listen(PORT, () => {
  console.log(`Server running on port ${PORT}`);
});