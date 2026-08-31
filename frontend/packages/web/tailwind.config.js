/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        mepbm: {
          gold: '#FFD700',
          iron: '#708090',
          blood: '#8B0000',
          forest: '#228B22',
          shadow: '#1a1a2e',
        },
      },
    },
  },
  plugins: [],
};
