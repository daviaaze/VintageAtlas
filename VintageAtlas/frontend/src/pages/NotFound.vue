<template>
  <div class="not-found">
    <div class="not-found-content">
      <h1>404</h1>
      <h2>Page Not Found</h2>
      <p>The page you are looking for doesn't exist or has been moved.</p>
      <router-link to="/" class="back-link">Return to Map</router-link>
    </div>
  </div>
</template>

<style scoped>
.not-found {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100vh;
  text-align: center;
}

.not-found-content {
  max-width: 500px;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  background-color: #fff;
}

h1 {
  font-size: 6rem;
  margin: 0;
  color: #dc3545;
}

h2 {
  margin-top: 0;
}

.back-link {
  display: inline-block;
  margin-top: 1rem;
  padding: 0.5rem 1rem;
  color: #fff;
  background-color: #007bff;
  border-radius: 4px;
  text-decoration: none;
  transition: background-color 0.3s;
}

.back-link:hover {
  background-color: #0069d9;
}
</style>
