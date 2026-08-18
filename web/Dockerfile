# Web image. Build context is web/; the SPA is static files behind nginx, and every
# /api + atlas request is routed to the API by the cluster's reverse proxy, not here.
FROM node:24-alpine AS build
WORKDIR /source
COPY package.json package-lock.json ./
RUN npm ci
COPY . ./
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.29-alpine
LABEL org.opencontainers.image.source=https://github.com/Kombushi/Craftiger
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /source/dist /usr/share/nginx/html
EXPOSE 8080
