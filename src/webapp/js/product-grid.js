/**
 * Product Grid Component
 * A reusable component to display products in a grid with search functionality
 */
class ProductGrid {
    constructor(containerId, apiBaseUrl) {
        this.container = document.getElementById(containerId);
        this.apiBaseUrl = apiBaseUrl;
        this.apiVersion = 'v1';
        this.lastSearchQuery = '';
        this.lastEtag = null;
        this.gridTemplate = document.getElementById('product-grid-template');
        this.rowTemplate = document.getElementById('product-row-template');
        this.noResultsTemplate = document.getElementById('no-results-template');
        this.requestTimingElement = document.getElementById('request-timing');
        
        // Initialize the grid
        this.init();
    }

    /**
     * Initialize the grid and event listeners
     */
    init() {
        this.renderEmptyGrid();
        
        // Add event listener for search button
        const searchButton = document.getElementById('search-button');
        const searchInput = document.getElementById('search-input');
        
        searchButton.addEventListener('click', () => {
            this.searchProducts(searchInput.value);
        });
        
        // Add event listener for Enter key in search input
        searchInput.addEventListener('keyup', (event) => {
            if (event.key === 'Enter') {
                this.searchProducts(searchInput.value);
            }
        });
        
        // Add event listener for clear cache button
        const clearCacheButton = document.getElementById('clear-cache-button');
        clearCacheButton.addEventListener('click', () => {
            this.clearCache();
        });
    }

    /**
     * Set the API version to use
     * @param {string} version - The API version (v1, v2, v3, v4)
     */
    setApiVersion(version) {
        this.apiVersion = version;
        this.lastEtag = null; // Reset ETag when changing API version
        
        // Update the UI to show the current version
        document.getElementById('current-api-version').textContent = version;
        
        // If there was a previous search, re-run it with the new version
        if (this.lastSearchQuery) {
            this.searchProducts(this.lastSearchQuery);
        }
    }

    /**
     * Render an empty grid
     */
    renderEmptyGrid() {
        this.container.innerHTML = '';
        const gridClone = this.gridTemplate.content.cloneNode(true);
        this.container.appendChild(gridClone);
    }

    /**
     * Search for products by category
     * @param {string} category - The category to search for
     */
    searchProducts(category) {
        if (!category || category.trim() === '') {
            alert('Please enter a category to search');
            return;
        }
        
        this.lastSearchQuery = category.trim();
        this.container.classList.add('loading');
        
        const url = `${this.apiBaseUrl}/${this.apiVersion}?category=${encodeURIComponent(category)}`;
        const startTime = performance.now();
        
        // Prepare headers for request
        const headers = new Headers();
        
        // Add ETag header for v5 endpoint
        if (this.apiVersion === 'v5' && this.lastEtag) {
            headers.append('If-None-Match', this.lastEtag);
        }
        
        fetch(url, { headers })
            .then(response => {
                const endTime = performance.now();
                const requestTime = Math.round(endTime - startTime);
                this.updateRequestTiming(requestTime);
                
                // Store ETag for v5 endpoint
                if (this.apiVersion === 'v5') {
                    const etag = response.headers.get('ETag');
                    if (etag) {
                        this.lastEtag = etag;
                    }
                }
                
                // Handle 304 Not Modified (cache hit for ETag)
                if (response.status === 304) {
                    alert('Data not modified since last request (ETag cache hit)');
                    this.container.classList.remove('loading');
                    return null;
                }
                
                if (!response.ok) {
                    throw new Error(`HTTP error! Status: ${response.status}`);
                }
                
                return response.json();
            })
            .then(data => {
                if (data === null) return; // Skip rendering for 304 responses
                
                this.renderProductGrid(data);
                this.container.classList.remove('loading');
            })
            .catch(error => {
                console.error('Error fetching products:', error);
                alert('Error fetching products. See console for details.');
                this.container.classList.remove('loading');
            });
    }

    /**
     * Render the product grid with data
     * @param {Array} products - The products to display
     */
    renderProductGrid(products) {
        this.renderEmptyGrid();
        
        const productList = document.getElementById('product-list');
        
        if (!products || products.length === 0) {
            const noResultsClone = this.noResultsTemplate.content.cloneNode(true);
            this.container.innerHTML = '';
            this.container.appendChild(noResultsClone);
            return;
        }
        
        products.forEach(product => {
            const rowClone = this.rowTemplate.content.cloneNode(true);
            
            rowClone.querySelector('.product-id').textContent = product.id;
            rowClone.querySelector('.product-name').textContent = product.name;
            rowClone.querySelector('.product-category').textContent = product.category;
            rowClone.querySelector('.product-description').textContent = product.description || 'N/A';
            rowClone.querySelector('.product-price').textContent = product.price.toFixed(2);
            
            // Add event listener for edit button
            const editButton = rowClone.querySelector('.edit-product');
            editButton.addEventListener('click', () => {
                this.handleEditProduct(product);
            });
            
            productList.appendChild(rowClone);
        });
    }

    /**
     * Handle edit product button click
     * @param {Object} product - The product to edit
     */
    handleEditProduct(product) {
        // This is just a placeholder for now
        console.log('Edit product:', product);
        alert(`Edit functionality for product "${product.name}" will be implemented later.`);
    }

    /**
     * Update the request timing display
     * @param {number} timeMs - The request time in milliseconds
     */
    updateRequestTiming(timeMs) {
        this.requestTimingElement.textContent = `Request time: ${timeMs} ms`;
    }

    /**
     * Clear the cache (just a simulation for demo purposes)
     */
    clearCache() {
        // For demonstration purposes only - this doesn't actually clear server cache
        // In a real application, you might have an endpoint to clear cache
        this.lastEtag = null;
        alert(`Cache cleared for ${this.apiVersion}. The next request will fetch fresh data.`);
        
        if (this.lastSearchQuery) {
            this.searchProducts(this.lastSearchQuery);
        }
    }
} 