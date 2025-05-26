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
        this.disableCacheCheckbox = document.getElementById('disable-cache-checkbox');
        
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
     * Check if cache should be disabled
     * @returns {boolean} - True if cache should be disabled
     */
    isCacheDisabled() {
        return this.disableCacheCheckbox && this.disableCacheCheckbox.checked;
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
            v4: 'Products',
            v5: 'Products'
        };
        
        const icons = {
            v1: 'bi-lightning-charge',
            v2: 'bi-clock-history',
            v3: 'bi-box',
            v4: 'bi-layers',
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
        
        // Use the original URL without timestamp to allow proper caching
        const url = `${this.apiBaseUrl}/${this.apiVersion}?category=${encodeURIComponent(category)}`;
        
        // Clear existing performance entries before making the request
        performance.clearResourceTimings();
        
        // Prepare headers for request
        const headers = new Headers();
        
        // Add ETag header for v5 endpoint
        if (this.apiVersion === 'v5' && this.lastEtag) {
            headers.append('If-None-Match', this.lastEtag);
        }
        
        // Add no-cache headers if checkbox is checked
        if (this.isCacheDisabled()) {
            headers.append('Cache-Control', 'no-cache, no-store, must-revalidate');
            headers.append('Pragma', 'no-cache');
            headers.append('Expires', '0');
            console.log('Cache disabled, adding no-cache headers');
        }
        
        // Start precise timing measurement as fallback
        const startTime = performance.now();
        
        fetch(url, { headers })
            .then(response => {
                const endTime = performance.now();
                // Calculate direct time as fallback
                const directTime = Math.round((endTime - startTime) * 10) / 10;
                
                // Get response status to detect cached responses
                const status = response.status;
                const isCached = status === 304 || (directTime < 10 && this.apiVersion !== 'v1');
                
                // Set a short timeout to let performance entries get recorded
                setTimeout(() => {
                    // Try to get timing from the Performance API
                    const perfEntries = performance.getEntriesByType('resource');
                    let networkTime = null;
                    
                    // Look for the most recent API request that matches our URL
                    for (let i = perfEntries.length - 1; i >= 0; i--) {
                        const entry = perfEntries[i];
                        if (entry.name.includes(this.apiBaseUrl) && 
                            entry.name.includes(this.apiVersion) && 
                            entry.name.includes(category)) {
                            // Found a matching entry
                            networkTime = Math.round(entry.duration * 10) / 10;
                            console.log('Found matching performance entry:', entry);
                            console.log('Network time from performance entry:', networkTime);
                            break;
                        }
                    }
                    
                    // For cached responses, we want to show very low times
                    if (isCached && (networkTime === null || networkTime > 10)) {
                        // For cached responses, use direct time if it's small enough
                        networkTime = Math.min(directTime, 5);
                        console.log('Using direct time for cached response:', networkTime);
                    } else if (networkTime === null || networkTime <= 0 || networkTime > 10000) {
                        // If we couldn't find a valid entry, use direct time as fallback
                        console.log('Using direct time as fallback:', directTime);
                        networkTime = directTime;
                    }
                    
                    // Update the UI with the timing
                    this.updateRequestTiming(networkTime);
                }, 50);
                
                // Store ETag for v5 endpoint
                if (this.apiVersion === 'v5') {
                    const etag = response.headers.get('ETag');
                    if (etag) {
                        this.lastEtag = etag;
                    }
                }
                
                // Handle 304 Not Modified (cache hit for ETag)
                if (status === 304) {
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
        // Create modal for editing
        const modalId = 'edit-product-modal';
        let modal = document.getElementById(modalId);
        
        // Remove existing modal if it exists
        if (modal) {
            modal.remove();
        }
        
        // Create new modal
        modal = document.createElement('div');
        modal.id = modalId;
        modal.className = 'modal fade';
        modal.tabIndex = -1;
        modal.setAttribute('aria-labelledby', 'editProductModalLabel');
        modal.setAttribute('aria-hidden', 'true');
        
        // Create modal content
        modal.innerHTML = `
            <div class="modal-dialog">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title" id="editProductModalLabel">Edit Product</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body">
                        <form id="edit-product-form">
                            <input type="hidden" id="product-id" value="${product.id}">
                            <div class="mb-3">
                                <label for="product-name" class="form-label">Name</label>
                                <input type="text" class="form-control" id="product-name" value="${product.name}" required>
                            </div>
                            <div class="mb-3">
                                <label for="product-category" class="form-label">Category</label>
                                <input type="text" class="form-control" id="product-category" value="${product.category}" required>
                            </div>
                            <div class="mb-3">
                                <label for="product-description" class="form-label">Description</label>
                                <textarea class="form-control" id="product-description" rows="3">${product.description || ''}</textarea>
                            </div>
                            <div class="mb-3">
                                <label for="product-price" class="form-label">Price</label>
                                <input type="number" step="0.01" class="form-control" id="product-price" value="${product.price}" required>
                            </div>
                        </form>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                        <button type="button" class="btn btn-primary" id="save-product-btn">Save Changes</button>
                    </div>
                </div>
            </div>
        `;
        
        // Add modal to document
        document.body.appendChild(modal);
        
        // Initialize Bootstrap modal
        const modalInstance = new bootstrap.Modal(modal);
        modalInstance.show();
        
        // Add event listener for save button
        document.getElementById('save-product-btn').addEventListener('click', () => {
            this.saveProductChanges(modalInstance);
        });
    }
    
    /**
     * Save product changes
     * @param {bootstrap.Modal} modalInstance - The modal instance
     */
    saveProductChanges(modalInstance) {
        // Get form values
        const id = document.getElementById('product-id').value;
        const name = document.getElementById('product-name').value;
        const category = document.getElementById('product-category').value;
        const description = document.getElementById('product-description').value;
        const price = parseFloat(document.getElementById('product-price').value);
        
        // Validate form
        if (!name || !category || isNaN(price)) {
            this.showToast('Please fill in all required fields with valid values', 'warning');
            return;
        }
        
        // Create product object
        const updatedProduct = {
            name,
            category,
            description,
            price
        };
        
        // Show loading state
        const saveButton = document.getElementById('save-product-btn');
        const originalText = saveButton.textContent;
        saveButton.disabled = true;
        saveButton.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Saving...';
        
        // Send PUT request to update the product using the common update endpoint
        fetch(`${this.apiBaseUrl}/update/${id}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(updatedProduct)
        })
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }
            return response.json();
        })
        .then(data => {
            // Close modal
            modalInstance.hide();
            
            // Show success message
            this.showToast('Product updated successfully', 'success');
            
            // Refresh product list to show updated data
            if (this.lastSearchQuery) {
                this.searchProducts(this.lastSearchQuery);
            }
        })
        .catch(error => {
            console.error('Error updating product:', error);
            this.showToast('Error updating product. See console for details.', 'danger');
            
            // Reset button
            saveButton.disabled = false;
            saveButton.textContent = originalText;
        });
    }

    /**
     * Update the request timing display
     * @param {number} timeMs - The request time in milliseconds
     */
    updateRequestTiming(timeMs) {
        // Ensure the time is rounded to 1 decimal place for consistency
        const formattedTime = Math.round(timeMs * 10) / 10;
        
        this.requestTimingElement.textContent = `Request time: ${formattedTime} ms`;
        
        // Add color based on response time
        this.requestTimingElement.className = 'badge';
        if (formattedTime < 10) {
            this.requestTimingElement.classList.add('bg-success');
        } else if (formattedTime < 50) {
            this.requestTimingElement.classList.add('bg-info');
        } else if (formattedTime < 150) {
            this.requestTimingElement.classList.add('bg-warning');
        } else {
            this.requestTimingElement.classList.add('bg-danger');
        }
    }

    /**
     * Clear the cache by calling the backend endpoint
     */
    clearCache() {
        // Show loading state on the button
        const clearCacheButton = document.getElementById('clear-cache-button');
        const originalText = clearCacheButton.innerHTML;
        clearCacheButton.disabled = true;
        clearCacheButton.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Clearing...';
        
        // Call the clear-cache endpoint
        fetch(`${this.apiBaseUrl}/clear-cache`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        })
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }
            return response.json();
        })
        .then(data => {
            // Reset the ETag value
            this.lastEtag = null;
            
            // Show success message
            this.showToast('Cache cleared successfully. Click "Search" to fetch fresh data.', 'success');
            
            // Don't automatically reload data - let the user do it manually
        })
        .catch(error => {
            console.error('Error clearing cache:', error);
            this.showToast('Error clearing cache. See console for details.', 'danger');
        })
        .finally(() => {
            // Reset button state
            clearCacheButton.disabled = false;
            clearCacheButton.innerHTML = originalText;
        });
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