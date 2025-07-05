import requests
import html2text
from urllib.parse import urlparse
import os
from bs4 import BeautifulSoup
import re

def download_wikipedia_to_markdown(wikipedia_url: str, output_file: str) -> bool:
    """
    Download a Wikipedia page and save it as markdown format.
    
    Args:
        wikipedia_url (str): The URL of the Wikipedia page
        output_file (str): The destination file path for the markdown content
        
    Returns:
        bool: True if successful, False otherwise
    """
    try:
        response = requests.get(wikipedia_url)
        response.raise_for_status()
        html = response.text

        # Extract the content inside the node with id="content"
        soup = BeautifulSoup(html, "html.parser")
        content_div = soup.find(id="mw-content-text")
        if not content_div:
            print("Could not find content with id='mw-content-text'")
            return False

        # before extracting data we proceed to remove thing that are not interesting
        # first of all remove table with style sidebar
        sidebar = content_div.find('table', {'class': 'sidebar'})
        if sidebar:
            sidebar.decompose()  # Remove the sidebar table

        # Now find a node with the text "See also" and remove it and all subsequent nodes.
        see_also = content_div.find(text="See also")
        if see_also:
            # Remove the "See also" node and all following siblings
            for sibling in see_also.find_all_next():
                sibling.decompose()
            see_also.decompose()  # Also remove the "See also" text itself

        # Extract only interesting content: paragraphs and headers
        interesting_elements = []
        
        stop_contents = ['See also']

        # Find all paragraphs and header divs, if you find a node with the content is exactly a stop
        # content, stop processing further
        for element in content_div.find_all(['p', 'div']):
                
            # Include paragraphs that have text content
            if element.name == 'p' and element.get_text(strip=True):
                interesting_elements.append(element)
            # Include divs that contain headers (h1, h2, h3, etc.)
            elif element.name == 'div' and element.find(['h1', 'h2', 'h3', 'h4', 'h5', 'h6']):
                interesting_elements.append(element)
            # Check if the element contains a stop content
            if element.get_text(strip=True) in stop_contents:
                break

        # now cycle  through the interesting elements and convert them to markdown
        markdown = ""
        h = html2text.HTML2Text()
        h.ignore_links = True  # Ignore links in the markdown output
        h.body_width = 0  # Don't wrap lines
        h.single_line_break = True  # Use single line breaks instead of double
        for element in interesting_elements:
            # Convert the HTML element to markdown
            element_markdown = h.handle(str(element)).strip()
            markdown += element_markdown + "\n\n"

        # Post-process markdown to ensure headers have proper spacing
        lines = markdown.split('\n')
        processed_lines = []
        
        for i, line in enumerate(lines):
            if line.startswith('#'):
                # Check if there are two empty lines before this header
                if i >= 2 and not (lines[i-1] == '' and lines[i-2] == ''):
                    # Add necessary empty lines
                    if i > 0 and lines[i-1] != '':
                        processed_lines.append('')
                    if i > 1 and lines[i-1] == '' and lines[i-2] != '':
                        processed_lines.append('')
                    elif i <= 1:
                        processed_lines.append('')
                        processed_lines.append('')
            processed_lines.append(line)
        
        markdown = '\n'.join(processed_lines)

        # Remove citation numbers in square brackets
        markdown = re.sub(r'\[\d+\]', '', markdown)
        
        # Remove image references (markdown image syntax)
        markdown = re.sub(r'!\[.*?\]\(.*?\)', '', markdown)

        # Write to output file
        with open(output_file, "w", encoding="utf-8", newline='\n') as f:
            f.write(markdown)
        return True

    except Exception as e:
        print(f"Error downloading Wikipedia page: {str(e)}")
        return False

def get_page_title_from_url(wikipedia_url: str) -> str:
    """
    Extract the page title from a Wikipedia URL for use as filename.
    
    Args:
        wikipedia_url (str): The Wikipedia URL
        
    Returns:
        str: The page title suitable for use as filename
    """
    try:
        parsed_url = urlparse(wikipedia_url)
        path_parts = parsed_url.path.split('/')
        if 'wiki' in path_parts:
            wiki_index = path_parts.index('wiki')
            if wiki_index + 1 < len(path_parts):
                title = path_parts[wiki_index + 1]
                # Replace URL encoding and make filename-safe
                title = title.replace('_', ' ')
                title = requests.utils.unquote(title)
                # Remove invalid filename characters
                invalid_chars = '<>:"/\\|?*'
                for char in invalid_chars:
                    title = title.replace(char, '')
                return title
    except Exception:
        pass
    
    return "wikipedia_page"
    return "wikipedia_page"
