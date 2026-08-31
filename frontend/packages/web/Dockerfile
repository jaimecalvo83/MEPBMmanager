FROM node:20-alpine AS builder

WORKDIR /app

COPY packages/shared/package.json ./packages/shared/
COPY packages/web/package.json ./packages/web/
COPY package.json ./

RUN npm install

COPY packages/shared/ ./packages/shared/
COPY packages/web/ ./packages/web/

RUN cd packages/shared && npm run build
RUN cd packages/web && npm run build

FROM nginx:alpine AS runner

COPY --from=builder /app/packages/web/dist /usr/share/nginx/html

COPY packages/web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
