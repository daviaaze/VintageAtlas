import { apiClient } from './client';

/**
 * Get historical data snapshots
 * @param options Query parameters
 */
export async function getHistoricalData(options = {}) {
  try {
    // Try to get data from API
    return await apiClient.get('/historical', { params: options });
  } catch (error) {
    // If API fails, load from local JSON file
    console.log('Loading historical data from local file');
    const response = await fetch('/data/historical-snapshots.json');
    if (!response.ok) {
      throw new Error('Failed to load historical data');
    }
    return await response.json();
  }
}

/**
 * Get historical data for a specific date range
 * @param startDate Start date (ISO format)
 * @param endDate End date (ISO format)
 */
export async function getHistoricalRange(startDate: string, endDate: string) {
  try {
    // Try to get data from API
    return await apiClient.get('/historical/range', {
      params: { start: startDate, end: endDate }
    });
  } catch (error) {
    // If API fails, load from local JSON file and filter by date
    console.log('Loading historical range data from local file');
    const response = await fetch('/data/historical-snapshots.json');
    if (!response.ok) {
      throw new Error('Failed to load historical data');
    }
    
    const allData = await response.json();
    
    // Filter by date range
    return allData.filter((snapshot: any) => {
      const timestamp = new Date(snapshot.timestamp).getTime();
      const start = startDate ? new Date(startDate).getTime() : 0;
      const end = endDate ? new Date(endDate).getTime() : Infinity;
      
      return timestamp >= start && timestamp <= end;
    });
  }
}
