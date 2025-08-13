import React, { useState, useEffect, useCallback } from 'react';
import './App.css';

const API_BASE = '';

function App() {
  const [courseStructure, setCourseStructure] = useState(null);
  const [currentView, setCurrentView] = useState({ type: 'root' });
  const [currentContent, setCurrentContent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [currentSlideIndex, setCurrentSlideIndex] = useState(0);
  const [slides, setSlides] = useState([]);
  const [showTOC, setShowTOC] = useState(false);

  useEffect(() => {
    fetchCourseStructure();
  }, []);

  const fetchCourseStructure = async () => {
    try {
      const response = await fetch(`${API_BASE}/api/course-structure`);
      const structure = await response.json();
      setCourseStructure(structure);
      
      if (structure.root) {
        setCurrentContent(structure.root);
        setCurrentView({ type: 'root' });
      }
      setLoading(false);
    } catch (error) {
      console.error('Error fetching course structure:', error);
      setLoading(false);
    }
  };

  const fetchContent = async (type, id, lessonId) => {
    try {
      setLoading(true);
      let url = `${API_BASE}/api/content/${type}`;
      if (id) url += `/${id}`;
      if (lessonId) url += `/${lessonId}`;
      
      const response = await fetch(url);
      const content = await response.json();
      
      if (type === 'slides') {
        setSlides(content);
        setCurrentSlideIndex(0);
        setCurrentContent(content[0] || null);
      } else {
        setCurrentContent(content);
        setSlides([]);
        setCurrentSlideIndex(0);
      }
      
      setLoading(false);
    } catch (error) {
      console.error('Error fetching content:', error);
      setLoading(false);
    }
  };

  const handleClick = useCallback(() => {
    if (loading || !courseStructure) return;

    const { type, moduleId, lessonId } = currentView;

    switch (type) {
      case 'root':
        if (courseStructure.modules && courseStructure.modules.length > 0) {
          const firstModule = courseStructure.modules[0];
          const moduleId = firstModule.metadata.id || firstModule.filename.replace('.module.md', '');
          setCurrentView({ type: 'module', moduleId });
          setCurrentContent(firstModule);
        }
        break;

      case 'module':
        const lessons = courseStructure.lessons[moduleId] || [];
        if (lessons.length > 0) {
          const firstLesson = lessons[0];
          setCurrentView({ type: 'lesson', moduleId, lessonId: firstLesson.id });
          setCurrentContent(firstLesson);
        }
        break;

      case 'lesson':
        fetchContent('slides', moduleId, lessonId);
        setCurrentView({ type: 'slides', moduleId, lessonId });
        break;

      case 'slides':
        if (currentSlideIndex < slides.length - 1) {
          const nextIndex = currentSlideIndex + 1;
          setCurrentSlideIndex(nextIndex);
          setCurrentContent(slides[nextIndex]);
        } else {
          const lessons = courseStructure.lessons[moduleId] || [];
          const currentLessonIndex = lessons.findIndex(l => l.id === lessonId);
          
          if (currentLessonIndex < lessons.length - 1) {
            const nextLesson = lessons[currentLessonIndex + 1];
            setCurrentView({ type: 'lesson', moduleId, lessonId: nextLesson.id });
            setCurrentContent(nextLesson);
          } else {
            const currentModuleIndex = courseStructure.modules.findIndex(m => 
              (m.metadata.id || m.filename.replace('.module.md', '')) === moduleId
            );
            
            if (currentModuleIndex < courseStructure.modules.length - 1) {
              const nextModule = courseStructure.modules[currentModuleIndex + 1];
              const nextModuleId = nextModule.metadata.id || nextModule.filename.replace('.module.md', '');
              setCurrentView({ type: 'module', moduleId: nextModuleId });
              setCurrentContent(nextModule);
            }
          }
        }
        break;

      default:
        break;
    }
  }, [currentView, courseStructure, loading, currentSlideIndex, slides]);

  useEffect(() => {
    const handleKeyPress = (e) => {
      if (e.code === 'Space' || e.key === 'ArrowRight' || e.key === 'Enter') {
        e.preventDefault();
        handleClick();
      }
    };

    window.addEventListener('keydown', handleKeyPress);
    return () => window.removeEventListener('keydown', handleKeyPress);
  }, [handleClick]);

  if (loading) {
    return <div className="loading">Loading...</div>;
  }

  const hasNoContent = !currentContent;
  const autoShowTOC = hasNoContent || showTOC;

  const getProgressInfo = () => {
    const { type, moduleId, lessonId } = currentView;
    
    switch (type) {
      case 'root':
        return 'Course Overview';
      case 'module':
        if (courseStructure?.modules) {
          const currentModuleIndex = courseStructure.modules.findIndex(m => 
            (m.metadata.id || m.filename.replace('.module.md', '')) === moduleId
          );
          return `Module ${currentModuleIndex + 1} of ${courseStructure.modules.length}`;
        }
        break;
      case 'lesson':
        if (courseStructure?.lessons?.[moduleId]) {
          const lessons = courseStructure.lessons[moduleId];
          const currentLessonIndex = lessons.findIndex(l => l.id === lessonId);
          return `Lesson ${currentLessonIndex + 1} of ${lessons.length}`;
        }
        break;
      case 'slides':
        if (slides.length > 0) {
          return `Slide ${currentSlideIndex + 1} of ${slides.length}`;
        }
        break;
    }
    
    return '';
  };

  const canGoBack = () => {
    const { type, moduleId, lessonId } = currentView;
    
    switch (type) {
      case 'root':
        return false; // Root is the only item at this level
      case 'module':
        const currentModuleIndex = courseStructure?.modules?.findIndex(m => 
          (m.metadata.id || m.filename.replace('.module.md', '')) === moduleId
        ) || 0;
        return currentModuleIndex > 0;
      case 'lesson':
        const lessons = courseStructure?.lessons?.[moduleId] || [];
        const currentLessonIndex = lessons.findIndex(l => l.id === lessonId);
        return currentLessonIndex > 0;
      case 'slides':
        return currentSlideIndex > 0;
      default:
        return false;
    }
  };

  const canGoForward = () => {
    const { type, moduleId, lessonId } = currentView;
    
    switch (type) {
      case 'root':
        return false; // Root is the only item at this level
      case 'module':
        const currentModuleIndex = courseStructure?.modules?.findIndex(m => 
          (m.metadata.id || m.filename.replace('.module.md', '')) === moduleId
        ) || 0;
        return currentModuleIndex < (courseStructure?.modules?.length || 0) - 1;
      case 'lesson':
        const lessons = courseStructure?.lessons?.[moduleId] || [];
        const currentLessonIndex = lessons.findIndex(l => l.id === lessonId);
        return currentLessonIndex < lessons.length - 1;
      case 'slides':
        return currentSlideIndex < slides.length - 1;
      default:
        return false;
    }
  };

  const goBack = () => {
    if (!canGoBack()) return;
    
    const { type, moduleId, lessonId } = currentView;
    
    switch (type) {
      case 'module':
        const currentModuleIndex = courseStructure.modules.findIndex(m => 
          (m.metadata.id || m.filename.replace('.module.md', '')) === moduleId
        );
        const prevModule = courseStructure.modules[currentModuleIndex - 1];
        const prevModuleId = prevModule.metadata.id || prevModule.filename.replace('.module.md', '');
        setCurrentView({ type: 'module', moduleId: prevModuleId });
        setCurrentContent(prevModule);
        break;
      case 'lesson':
        const lessons = courseStructure.lessons[moduleId] || [];
        const currentLessonIndex = lessons.findIndex(l => l.id === lessonId);
        const prevLesson = lessons[currentLessonIndex - 1];
        setCurrentView({ type: 'lesson', moduleId, lessonId: prevLesson.id });
        setCurrentContent(prevLesson);
        break;
      case 'slides':
        const prevIndex = currentSlideIndex - 1;
        setCurrentSlideIndex(prevIndex);
        setCurrentContent(slides[prevIndex]);
        break;
    }
  };

  const goForward = () => {
    if (!canGoForward()) return;
    
    const { type, moduleId, lessonId } = currentView;
    
    switch (type) {
      case 'module':
        const currentModuleIndex = courseStructure.modules.findIndex(m => 
          (m.metadata.id || m.filename.replace('.module.md', '')) === moduleId
        );
        const nextModule = courseStructure.modules[currentModuleIndex + 1];
        const nextModuleId = nextModule.metadata.id || nextModule.filename.replace('.module.md', '');
        setCurrentView({ type: 'module', moduleId: nextModuleId });
        setCurrentContent(nextModule);
        break;
      case 'lesson':
        const lessons = courseStructure.lessons[moduleId] || [];
        const currentLessonIndex = lessons.findIndex(l => l.id === lessonId);
        const nextLesson = lessons[currentLessonIndex + 1];
        setCurrentView({ type: 'lesson', moduleId, lessonId: nextLesson.id });
        setCurrentContent(nextLesson);
        break;
      case 'slides':
        const nextIndex = currentSlideIndex + 1;
        setCurrentSlideIndex(nextIndex);
        setCurrentContent(slides[nextIndex]);
        break;
    }
  };

  const navigateToRoot = () => {
    setCurrentView({ type: 'root' });
    setCurrentContent(courseStructure.root);
    setSlides([]);
    setCurrentSlideIndex(0);
    setShowTOC(false);
  };

  const navigateToModule = (moduleId) => {
    const module = courseStructure.modules.find(m => 
      (m.metadata.id || m.filename.replace('.module.md', '')) === moduleId
    );
    if (module) {
      setCurrentView({ type: 'module', moduleId });
      setCurrentContent(module);
      setSlides([]);
      setCurrentSlideIndex(0);
      setShowTOC(false);
    }
  };

  const navigateToLesson = (moduleId, lessonId) => {
    const lessons = courseStructure.lessons[moduleId] || [];
    const lesson = lessons.find(l => l.id === lessonId);
    if (lesson) {
      setCurrentView({ type: 'lesson', moduleId, lessonId });
      setCurrentContent(lesson);
      setSlides([]);
      setCurrentSlideIndex(0);
      setShowTOC(false);
    }
  };

  const navigateToSlide = async (moduleId, lessonId, slideIndex) => {
    try {
      // First, make sure we have the slides data
      let slidesData = courseStructure.slides[lessonId];
      
      if (!slidesData || slidesData.length === 0) {
        // Try to fetch slides from the API
        await fetchContent('slides', moduleId, lessonId);
        slidesData = slides; // Use the slides from state after fetching
      }
      
      if (slidesData && slidesData.length > 0 && slideIndex < slidesData.length) {
        setSlides(slidesData);
        setCurrentSlideIndex(slideIndex);
        setCurrentContent(slidesData[slideIndex]);
        setCurrentView({ type: 'slides', moduleId, lessonId });
        setShowTOC(false);
      } else {
        console.error('No slides available for this lesson');
      }
    } catch (error) {
      console.error('Error navigating to slide:', error);
    }
  };

  const getBackgroundStyle = () => {
    const metadata = currentContent?.metadata;
    let style = {};
    
    // Handle background color first
    if (metadata?.['background-color']) {
      const bgColor = metadata['background-color'];
      if (bgColor.startsWith('#') || /^[a-zA-Z]+$/.test(bgColor)) {
        const textColor = isLightColor(bgColor) ? '#1a1a1a' : '#ffffff';
        style.background = bgColor;
        style.color = textColor;
      }
    }
    
    // Handle background image
    if (metadata?.['background-image'] && currentContent?.fileDir) {
      const imagePath = metadata['background-image'];
      const fullImagePath = `${API_BASE}/api/images/${currentContent.fileDir}/${imagePath}`;
      
      // Handle positioning with adaptive sizing
      const position = metadata['background-image-position']?.toLowerCase() || 'center';
      let backgroundPosition, backgroundSize;
      
      switch (position) {
        case 'left':
          backgroundPosition = 'left center';
          backgroundSize = 'auto 100vh'; // Height fits screen, width auto-scales
          break;
        case 'right':
          backgroundPosition = 'right center';
          backgroundSize = 'auto 100vh'; // Height fits screen, width auto-scales
          break;
        case 'top':
          backgroundPosition = 'center top';
          backgroundSize = '100vw auto'; // Width fits screen, height auto-scales
          break;
        case 'bottom':
          backgroundPosition = 'center bottom';
          backgroundSize = '100vw auto'; // Width fits screen, height auto-scales
          break;
        case 'center':
        default:
          backgroundPosition = 'center center';
          backgroundSize = 'cover'; // Covers entire page while maintaining aspect ratio
      }
      
      // Handle transparency (0-100, where 0 is normal, 100 is invisible)
      if (metadata['background-image-transparency']) {
        const transparency = parseInt(metadata['background-image-transparency']) || 0;
        const opacity = Math.max(0, Math.min(1, (100 - transparency) / 100));
        
        console.log(`Transparency: ${transparency}, Opacity: ${opacity}`); // Debug log
        
        // Create an overlay div with the image and apply opacity to it
        style.position = 'relative';
        style.backgroundImageOverlay = {
          backgroundImage: `url("${fullImagePath}")`,
          backgroundRepeat: 'no-repeat',
          backgroundSize: backgroundSize,
          backgroundPosition: backgroundPosition,
          opacity: opacity,
          position: 'absolute',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          zIndex: 0, // Changed from -1 to 0
          pointerEvents: 'none' // Ensure it doesn't block interactions
        };
      } else {
        // Normal background image without transparency
        style.backgroundImage = `url("${fullImagePath}")`;
        style.backgroundRepeat = 'no-repeat';
        style.backgroundSize = backgroundSize;
        style.backgroundPosition = backgroundPosition;
      }
      
      // If we have a background image, ensure good text contrast (but only if no color was set)
      if (!metadata?.['background-color']) {
        style.color = '#ffffff';
        style.textShadow = '2px 2px 4px rgba(0,0,0,0.7)';
      }
    }
    
    return style;
  };

  const isLightColor = (color) => {
    // Simple heuristic for common cases
    const lightColors = ['white', 'yellow', 'cyan', 'lime', 'pink', 'orange', 'lightblue', 'lightgreen', 'lightgray', 'silver'];
    
    if (lightColors.includes(color.toLowerCase())) return true;
    
    // For hex codes, check luminance
    if (color.startsWith('#')) {
      const hex = color.replace('#', '');
      if (hex.length === 3) {
        const r = parseInt(hex[0] + hex[0], 16);
        const g = parseInt(hex[1] + hex[1], 16);
        const b = parseInt(hex[2] + hex[2], 16);
        return (r + g + b) > 384; // Roughly middle luminance
      } else if (hex.length === 6) {
        const r = parseInt(hex.substr(0, 2), 16);
        const g = parseInt(hex.substr(2, 2), 16);
        const b = parseInt(hex.substr(4, 2), 16);
        return (r + g + b) > 384;
      }
    }
    
    return false; // Default to dark text for unknown colors
  };

  const isCurrentItem = (type, moduleId, lessonId, slideIndex) => {
    const current = currentView;
    switch (type) {
      case 'root':
        return current.type === 'root';
      case 'module':
        return current.type === 'module' && current.moduleId === moduleId;
      case 'lesson':
        return current.type === 'lesson' && current.moduleId === moduleId && current.lessonId === lessonId;
      case 'slide':
        return current.type === 'slides' && current.moduleId === moduleId && current.lessonId === lessonId && currentSlideIndex === slideIndex;
      default:
        return false;
    }
  };


  const backgroundStyle = getBackgroundStyle();
  const overlayStyle = backgroundStyle.backgroundImageOverlay;
  delete backgroundStyle.backgroundImageOverlay; // Remove from main style

  return (
    <div 
      className={`App level-${currentView.type} ${hasNoContent ? 'no-content' : ''}`} 
      style={backgroundStyle}
      onClick={!hasNoContent ? handleClick : undefined}
    >
      {overlayStyle && (
        <div 
          className="background-image-overlay" 
          style={overlayStyle}
        />
      )}
      
      {/* TOC Toggle Button - hide when auto-showing TOC */}
      {!hasNoContent && (
        <button 
          className={`toc-toggle-button ${showTOC ? 'moved' : ''}`}
          onClick={(e) => { e.stopPropagation(); setShowTOC(!showTOC); }}
          title="Table of Contents"
        >
          ☰
        </button>
      )}
      
      {/* TOC Sidebar */}
      <div className={`toc-sidebar ${autoShowTOC ? 'visible' : ''} ${hasNoContent ? 'sticky' : ''}`}>
        <div className="toc-header">
          <h3>Table of Contents</h3>
          {!hasNoContent && (
            <button 
              className="toc-close-button"
              onClick={(e) => { e.stopPropagation(); setShowTOC(false); }}
            >
              ✕
            </button>
          )}
        </div>
        <div className="toc-content">
          {courseStructure ? (
            <>
              {/* Root */}
              <div 
                className={`toc-item toc-root ${isCurrentItem('root') ? 'current' : ''}`}
                onClick={(e) => { e.stopPropagation(); navigateToRoot(); }}
              >
                📚 {courseStructure.root?.metadata?.title || 'Course'}
              </div>

              {/* Modules */}
              {courseStructure.modules.map((module, moduleIndex) => {
                const moduleId = module.metadata.id || module.filename.replace('.module.md', '');
                const lessons = courseStructure.lessons[moduleId] || [];
                
                return (
                  <div key={moduleId} className="toc-module">
                    <div 
                      className={`toc-item toc-module-header ${isCurrentItem('module', moduleId) ? 'current' : ''}`}
                      onClick={(e) => { e.stopPropagation(); navigateToModule(moduleId); }}
                    >
                      📖 {module.metadata?.title || `Module ${moduleIndex + 1}`}
                    </div>
                    
                    {/* Lessons */}
                    <div className="toc-lessons">
                      {lessons.map((lesson, lessonIndex) => {
                        const slides = courseStructure.slides[lesson.id] || [];
                        
                        return (
                          <div key={lesson.id} className="toc-lesson">
                            <div 
                              className={`toc-item toc-lesson-header ${isCurrentItem('lesson', moduleId, lesson.id) ? 'current' : ''}`}
                              onClick={(e) => { e.stopPropagation(); navigateToLesson(moduleId, lesson.id); }}
                            >
                              📄 {lesson.metadata?.title || `Lesson ${lessonIndex + 1}`}
                            </div>
                            
                            {/* Slides */}
                            {slides.length > 0 && (
                              <div className="toc-slides">
                                {slides.map((slide, slideIndex) => (
                                  <div 
                                    key={slideIndex}
                                    className={`toc-item toc-slide ${isCurrentItem('slide', moduleId, lesson.id, slideIndex) ? 'current' : ''}`}
                                    onClick={(e) => { e.stopPropagation(); navigateToSlide(moduleId, lesson.id, slideIndex); }}
                                  >
                                    ▶ {slide.metadata?.title || `Slide ${slideIndex + 1}`}
                                  </div>
                                ))}
                              </div>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                );
              })}
            </>
          ) : (
            <div className="toc-loading">Loading course structure...</div>
          )}
        </div>
      </div>
      
      {/* TOC Overlay - only show when manually opened and content exists */}
      {showTOC && !hasNoContent && (
        <div 
          className="toc-overlay" 
          onClick={() => setShowTOC(false)}
        />
      )}
      
      {/* Main content area */}
      <div className={`main-content ${hasNoContent || autoShowTOC ? 'with-sidebar' : ''}`}>
        {!hasNoContent && (
          <div className="navigation-pane">
            <div className="progress-info">{getProgressInfo()}</div>
            <div className="navigation-buttons">
              <button 
                className="nav-button back-button" 
                onClick={(e) => { e.stopPropagation(); goBack(); }}
                disabled={!canGoBack()}
                title="Go Back"
              >
                ←
              </button>
              <button 
                className="nav-button forward-button" 
                onClick={(e) => { e.stopPropagation(); goForward(); }}
                disabled={!canGoForward()}
                title="Go Forward"
              >
                →
              </button>
            </div>
          </div>
        )}
        
        <div className="content-container">
          {hasNoContent ? (
            <div className="error-with-toc">
              <h2>No Content Available</h2>
              <p>Use the table of contents on the left to navigate to available content.</p>
            </div>
          ) : (
            <div 
              className="content" 
              dangerouslySetInnerHTML={{ __html: currentContent.displayContent }}
            />
          )}
        </div>
        
        {!hasNoContent && (
          <div className="navigation-hint">Click anywhere to continue</div>
        )}
      </div>
    </div>
  );
}

export default App;