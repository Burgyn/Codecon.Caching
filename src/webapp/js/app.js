/**
 * Main Application JavaScript
 * Initializes the product grid component and manages navigation
 */
document.addEventListener('DOMContentLoaded', function() {
    // Configuration
    const apiBaseUrl = 'http://localhost:5000/api/products';
    const descriptions = {
        v1: 'Get products by category - without caching',
        v2: 'Get products by category - with output caching',
        v3: 'Get products by category - with output caching ETag'
    };

    // Initialize product grid component
    const productGrid = new ProductGrid('product-grid-container', apiBaseUrl);
    
    // Update page title and description based on selected version
    function updatePageInfo(version) {
        const titles = {
            v1: 'Products (No Caching)',
            v2: 'Products (Output Cache)',
            v3: 'Products (ETag Caching)'
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
    
    // Optional: Add some example categories as quick search buttons
    const exampleCategories = ['Electronics', 'Books', 'Clothing', 'Food'];
    const searchInput = document.getElementById('search-input');
    
    // Initialize with one example category
    searchInput.value = 'Electronics';
    productGrid.searchProducts('Electronics');
}); 