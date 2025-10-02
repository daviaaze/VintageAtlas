import axios from 'axios';

// For development, we can use mock data when the server is not available
const useMockData = true; // Set to false when real API is available

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  }
});

// Mock data for development - will be loaded from JSON for historical data
const mockData = {
  status: {
    // Server info
    serverName: "VintageStory Dev Server",
    gameVersion: "1.18.15",
    modVersion: "1.0.0",
    currentPlayers: 3,
    maxPlayers: 20,
    uptime: 7245, // in seconds
    tps: 19.8,
    memoryUsage: 1024 * 1024 * 512, // 512 MB
    
    // Live data
    spawnPoint: { x: 512000.0, y: 116.0, z: 512000.0 },
    spawnTemperature: 4.6,
    spawnRainfall: 0.0,
    date: {
      year: 1,
      month: 10,
      day: 6,
      hour: 9,
      minute: 11
    },
    weather: {
      temperature: 4.6,
      rainfall: 0.0,
      windSpeed: 0.48
    },
    players: [
      {
        name: "caioasmuniz",
        uid: "caioasmunizID",
        coordinates: { x: 511471.0, y: 123.0, z: 519009.0 },
        health: { current: 21.0, max: 21.0 },
        hunger: { current: 979.8, max: 1500.0 },
        temperature: 6.7,
        bodyTemp: 45.0
      },
      { 
        name: "Explorer_Steve", 
        coordinates: { x: 511550.0, y: 125.0, z: 518950.0 },
        health: { current: 20, max: 20 },
        hunger: { current: 1200, max: 1500 },
        temperature: 5.8,
        bodyTemp: 36.7
      },
      { 
        name: "Builder_Jane", 
        coordinates: { x: 511634.0, y: 119.0, z: 519200.0 },
        health: { current: 20, max: 20 },
        hunger: { current: 1400, max: 1500 },
        temperature: 7.3,
        bodyTemp: 37.0
      }
    ],
    animals: [
      {
        type: "game:deer-fallow-adult-female",
        name: "Fallow deer (female)",
        coordinates: { x: 511359.0, y: 111.0, z: 518907.0 },
        health: { current: 21.0, max: 21.0 },
        temperature: 9.8,
        rainfall: 0.0,
        wind: { percent: 22.0 }
      },
      {
        type: "game:wolf-eurasian-adult-male",
        name: "Wolf (male)",
        coordinates: { x: 511509.0, y: 138.0, z: 518839.0 },
        health: { current: 15.0, max: 15.0 },
        temperature: 4.3,
        rainfall: 0.0,
        wind: { percent: 41.0 }
      },
      {
        type: "game:bear-brown-adult-female",
        name: "Brown bear (female)",
        coordinates: { x: 511342.0, y: 119.0, z: 519029.0 },
        health: { current: 64.0, max: 64.0 },
        temperature: 8.0,
        rainfall: 0.0,
        wind: { percent: 25.0 }
      },
      {
        type: "game:fox-red-adult-male",
        name: "Fox (male)",
        coordinates: { x: 511644.0, y: 121.0, z: 518990.0 },
        health: { current: 6.0, max: 6.0 },
        temperature: 6.5,
        rainfall: 0.16,
        wind: { percent: 45.0 }
      },
      {
        type: "game:chicken-rooster",
        name: "Rooster",
        coordinates: { x: 511378.0, y: 116.0, z: 519010.0 },
        health: { current: 3.0, max: 3.0 },
        temperature: 8.5,
        rainfall: 0.0,
        wind: { percent: 25.0 }
      }
    ]
  },
  players: [
    {
      name: "caioasmuniz", 
      uid: "caioasmunizID",
      coordinates: { x: 511471.0, y: 123.0, z: 519009.0 }
    },
    { 
      name: "Explorer_Steve", 
      coordinates: { x: 511550.0, y: 125.0, z: 518950.0 }
    },
    { 
      name: "Builder_Jane", 
      coordinates: { x: 511634.0, y: 119.0, z: 519200.0 }
    }
  ],
  historical: {
    // This will be loaded from JSON file
    snapshots: []
  }
};

// Get mock data by endpoint
function getMockData(url: string): any {
  if (url.includes('/status')) {
    if (url.includes('/players')) {
      return mockData.players;
    }
    return mockData.status;
  } 
  else if (url.includes('/historical')) {
    return mockData.historical.snapshots;
  }
  return null;
}

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
    // If we're using mock data and the request fails, return mock data
    if (useMockData && error.config && error.config.url) {
      console.log(`Using mock data for ${error.config.url}`);
      const mockResult = getMockData(error.config.url);
      if (mockResult) {
        return mockResult;
      }
    }
    
    const errorResponse = {
      status: error.response?.status || 500,
      message: error.response?.data?.message || 'Unknown error occurred',
      data: error.response?.data || null
    };
    
    console.error('API Error:', errorResponse);
    
    return Promise.reject(errorResponse);
  }
);
