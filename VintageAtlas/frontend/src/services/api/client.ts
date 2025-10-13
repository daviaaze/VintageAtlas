import axios from 'axios';

// For development, we can use mock data when the server is not available
export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  }
});

// Add a request interceptor
apiClient.interceptors.request.use(
  config => {
    // You can add authentication headers here if needed
    return config;
  },
  error => {
    return Promise.reject(error);
  }
);

// Add a response interceptor
apiClient.interceptors.response.use(
  response => {
    // Return the actual data instead of the axios response object
    return response.data;
  },
  error => {
    const errorResponse = {
      status: error.response?.status || 500,
      message: error.response?.data?.message || 'Unknown error occurred',
      data: error.response?.data || null
    };
    
    console.error('API Error:', errorResponse);
    
    return Promise.reject(errorResponse);
  }
);
