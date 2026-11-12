FROM node:22-alpine

WORKDIR /app

COPY package.json ./
COPY server.js ./
COPY public ./public
COPY kahoot_images ./kahoot_images

EXPOSE 3000

CMD ["node", "server.js"]

