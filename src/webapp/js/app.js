/**
 * Main Application JavaScript
 * Initializes the product grid component and manages navigation
 */
document.addEventListener('DOMContentLoaded', function() {
    // Configuration
    const apiBaseUrl = 'http://localhost:5000/api/products';
    const descriptions = {
        v1: 'Get products by category - without caching',
        v2: 'Get products by category - with response caching',
        v3: 'Get products by category - with output caching',
        v4: 'Get products by category - with hybrid cache (fusion)',
        v5: 'Get products by category - ETag cache with Delta'
    };

    // Initialize product grid component
    const productGrid = new ProductGrid('product-grid-container', apiBaseUrl);
    
    // Update page title and description based on selected version
    function updatePageInfo(version) {
        const titles = {
            v1: 'Products (No Caching)',
            v2: 'Products (Response Cache)',
            v3: 'Products (Output Cache)',
            v4: 'Products (Hybrid Cache)',
            v5: 'Products (ETag Caching)'
        };
        
        document.getElementById('current-page-title').textContent = titles[version] || titles.v1;
        document.getElementById('api-description').textContent = descriptions[version] || descriptions.v1;
    }
    
    // Setup navigation
    const navLinks = document.querySelectorAll('.nav-link[data-api-version]');
    
    navLinks.forEach(link => {
        link.addEventListener('click', function(e) {
            e.preventDefault();
            
            // Remove active class from all links
            navLinks.forEach(l => l.classList.remove('active'));
            
            // Add active class to clicked link
            this.classList.add('active');
            
            // Get API version from data attribute
            const version = this.getAttribute('data-api-version');
            
            // Update product grid component
            productGrid.setApiVersion(version);
            
            // Update page title and description
            updatePageInfo(version);
        });
    });
    
    // Categories for autocomplete from DatabaseInitializer.cs
    const categories = [
        "Electronics", "Clothing", "Home & Kitchen", "Books", "Sports", 
        "Toys", "Beauty", "Automotive", "Health", "Garden", "Furniture",
        "Jewelry", "Office", "Food", "Tools", "Baby", "Pet Supplies"
    ];
    
    // Setup autocomplete for search
    const searchInput = document.getElementById('search-input');
    
    // Make search input wider with important flag to override any existing styles
    searchInput.setAttribute('style', 'width: 350px !important; min-width: 350px !important; max-width: none !important');
    
    // Also try to modify the parent container if it exists
    if (searchInput.parentElement) {
        searchInput.parentElement.style.width = 'auto';
        searchInput.parentElement.style.maxWidth = 'none';
    }
    
    // Load Awesomplete CSS and JS if not already loaded
    if (!document.querySelector('link[href*="awesomplete.css"]')) {
        const awesompleteCSS = document.createElement('link');
        awesompleteCSS.rel = 'stylesheet';
        awesompleteCSS.href = 'https://cdnjs.cloudflare.com/ajax/libs/awesomplete/1.1.5/awesomplete.min.css';
        document.head.appendChild(awesompleteCSS);
    }
    
    function loadAwesomplete() {
        if (window.Awesomplete) {
            initAwesomplete();
        } else {
            const awesompleteScript = document.createElement('script');
            awesompleteScript.src = 'https://cdnjs.cloudflare.com/ajax/libs/awesomplete/1.1.5/awesomplete.min.js';
            awesompleteScript.onload = initAwesomplete;
            document.body.appendChild(awesompleteScript);
        }
    }
    
    function initAwesomplete() {
        // Initialize Awesomplete
        const awesomplete = new Awesomplete(searchInput, {
            list: categories,
            minChars: 0,
            maxItems: 15,
            autoFirst: true
        });
        
        // Add custom styling with !important to override existing styles
        const style = document.createElement('style');
        style.textContent = `
            #search-input {
                min-width: 350px !important;
                width: 350px !important;
                max-width: none !important;
                padding: 8px 12px !important;
                border-radius: 4px !important;
                border: 1px solid #ced4da !important;
                font-size: 16px !important;
                box-sizing: border-box !important;
            }
            
            .awesomplete {
                position: relative !important;
                width: auto !important;
                max-width: none !important;
            }
            .awesomplete > ul {
                border-radius: 4px !important;
                box-shadow: 0 2px 10px rgba(0,0,0,0.1) !important;
                margin-top: 2px !important;
                padding: 0 !important;
                min-width: 350px !important;
                left: 0 !important;
                right: auto !important;
                width: auto !important;
                max-width: 100vw !important;
                z-index: 9999 !important;
            }
            .awesomplete > ul > li {
                padding: 10px 15px !important;
                transition: background-color 0.2s !important;
                white-space: nowrap !important;
            }
            .awesomplete > ul > li:hover {
                background-color: #f0f0f0 !important;
            }
            .awesomplete mark {
                background: rgba(66, 133, 244, 0.2) !important;
                color: inherit !important;
                font-weight: bold !important;
            }
        `;
        document.head.appendChild(style);
        
        // Reapply styles directly to the element to ensure they take effect
        setTimeout(() => {
            searchInput.style.width = '350px';
            searchInput.style.minWidth = '350px';
            searchInput.style.maxWidth = 'none';
        }, 100);
        
        // Show all options on focus
        searchInput.addEventListener('focus', function() {
            if (searchInput.value.length === 0) {
                awesomplete.evaluate();
                awesomplete.open();
            }
        });
        
        // Handle item selection
        searchInput.addEventListener('awesomplete-selectcomplete', function() {
            productGrid.searchProducts(searchInput.value);
        });
        
        // Handle enter key
        searchInput.addEventListener('keypress', function(e) {
            if (e.key === 'Enter') {
                productGrid.searchProducts(searchInput.value);
            }
        });
    }
    
    // Load Awesomplete
    loadAwesomplete();
    
    // Initialize with one example category
    searchInput.value = 'Electronics';
    productGrid.searchProducts('Electronics');
}); 