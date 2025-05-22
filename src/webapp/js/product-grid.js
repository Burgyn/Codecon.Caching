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
     * @param {string} version - The API version (v1, v2, v3, v5)
     */
    setApiVersion(version) {
        this.apiVersion = version;
        this.lastEtag = null; // Reset ETag when changing API version
        
        // Update the UI to show the current version
        const versionBadge = document.querySelector('#caching-info .badge');
        if (versionBadge) {
            versionBadge.textContent = version;
        }
        
        // Update page title based on version
        const pageTitles = {
            v1: 'Products',
            v2: 'Products',
            v3: 'Products',
            v5: 'Products'
        };
        
        const icons = {
            v1: 'bi-lightning-charge',
            v2: 'bi-clock-history',
            v3: 'bi-box',
            v5: 'bi-tag'
        };
        
        const titleEl = document.getElementById('current-page-title');
        if (titleEl) {
            const icon = document.createElement('i');
            icon.className = `bi ${icons[version] || 'bi-grid-3x3-gap'} me-2`;
            
            titleEl.innerHTML = '';
            titleEl.appendChild(icon);
            titleEl.appendChild(document.createTextNode(pageTitles[version] || 'Products'));
        }
        
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
            this.showToast('Please enter a category to search', 'warning');
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
                    this.showToast('Data not modified since last request (ETag cache hit)', 'success');
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
                this.showToast('Error fetching products. See console for details.', 'danger');
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
            
            // Update category to use badge
            const categoryBadge = rowClone.querySelector('.product-category .badge');
            if (categoryBadge) {
                categoryBadge.textContent = product.category;
                
                // Set different badge colors based on category
                const categoryClasses = {
                    'Toys': 'bg-primary',
                    'Electronics': 'bg-info',
                    'Clothing': 'bg-success',
                    'Books': 'bg-warning',
                    'Sports': 'bg-danger',
                    'Beauty': 'bg-secondary',
                    'Home & Kitchen': 'bg-dark'
                };
                
                const badgeClass = categoryClasses[product.category] || 'bg-secondary';
                categoryBadge.className = `badge ${badgeClass}`;
            }
            
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
        this.showToast(`Edit functionality for product "${product.name}" will be implemented later.`, 'info');
    }

    /**
     * Update the request timing display
     * @param {number} timeMs - The request time in milliseconds
     */
    updateRequestTiming(timeMs) {
        this.requestTimingElement.textContent = `Request time: ${timeMs} ms`;
        
        // Add color based on response time
        this.requestTimingElement.className = 'badge';
        if (timeMs < 50) {
            this.requestTimingElement.classList.add('bg-success');
        } else if (timeMs < 100) {
            this.requestTimingElement.classList.add('bg-info');
        } else if (timeMs < 200) {
            this.requestTimingElement.classList.add('bg-warning');
        } else {
            this.requestTimingElement.classList.add('bg-danger');
        }
    }

    /**
     * Clear the cache (just a simulation for demo purposes)
     */
    clearCache() {
        // For demonstration purposes only - this doesn't actually clear server cache
        // In a real application, you might have an endpoint to clear cache
        this.lastEtag = null;
        this.showToast(`Cache cleared for ${this.apiVersion}. The next request will fetch fresh data.`, 'warning');
        
        if (this.lastSearchQuery) {
            this.searchProducts(this.lastSearchQuery);
        }
    }
    
    /**
     * Show a toast notification
     * @param {string} message - The message to display
     * @param {string} type - The type of toast (success, warning, danger, info)
     */
    showToast(message, type = 'info') {
        // Create toast container if it doesn't exist
        let toastContainer = document.querySelector('.toast-container');
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            document.body.appendChild(toastContainer);
        }
        
        // Create toast element
        const toastId = `toast-${Date.now()}`;
        const toast = document.createElement('div');
        toast.className = `toast align-items-center border-0 show`;
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');
        toast.setAttribute('aria-atomic', 'true');
        toast.id = toastId;
        
        // Set background color based on type
        const bgClass = `bg-${type}`;
        if (type === 'warning' || type === 'info') {
            toast.classList.add(bgClass, 'text-dark');
        } else {
            toast.classList.add(bgClass, 'text-white');
        }
        
        // Create toast content
        const toastContent = `
            <div class="d-flex">
                <div class="toast-body">
                    ${message}
                </div>
                <button type="button" class="btn-close me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        `;
        toast.innerHTML = toastContent;
        
        // Add toast to container
        toastContainer.appendChild(toast);
        
        // Auto remove after 3 seconds
        setTimeout(() => {
            const toastElement = document.getElementById(toastId);
            if (toastElement) {
                toastElement.remove();
            }
        }, 3000);
    }
} 