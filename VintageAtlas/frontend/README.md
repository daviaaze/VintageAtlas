# VintageAtlas Frontend

A modern Vue.js-based frontend for the VintageAtlas mod.

## Features

- Modern Vue.js 3 with Composition API and TypeScript
- Interactive map display using OpenLayers
- Real-time server status updates
- Historical data visualization
- Dark/light theme support
- Responsive design for all screen sizes

## Development

### Prerequisites

- Node.js v18+ (v20+ recommended)
- npm or pnpm

### Setup

1. Install dependencies:
   ```
   npm install
   ```

2. Start development server:
   ```
   npm run dev
   ```

3. Open http://localhost:5173 in your browser

### Build for Production

```
npm run build
```

Built files will be placed in the `../html/dist` directory, which is served by the VintageAtlas mod's internal web server.

## Project Structure

```
frontend/
├── src/
│   ├── assets/          # Images, fonts, icons
│   ├── components/      # Vue components
│   │   ├── common/      # Generic UI components
│   │   ├── map/         # Map-specific components
│   │   └── ui/          # UI components
│   ├── composables/     # Vue composition functions
│   ├── layouts/         # Page layouts
│   ├── pages/           # Route pages
│   ├── stores/          # Pinia stores
│   ├── types/           # TypeScript type definitions
│   ├── utils/           # Utility functions
│   ├── services/        # API services
│   ├── App.vue          # Root component
│   └── main.ts          # App entry point
├── .env.*               # Environment variables
├── vite.config.ts       # Vite configuration
└── index.html           # HTML entry point
```

## API Endpoints

The frontend expects the following API endpoints to be available:

- `/api/status` - Server status information
- `/api/config` - Server configuration
- `/tiles/{z}/{x}/{y}.png` - Map tiles

These endpoints are proxied during development to the VintageAtlas mod's internal web server.

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature-name`
3. Commit your changes: `git commit -m 'Add some feature'`
4. Push to the branch: `git push origin feature-name`
5. Submit a pull request

## License

Same as the VintageAtlas mod