# Stage 1 builds the Angular app; stage 2 serves the static files with an unprivileged nginx (runs as non-root).
FROM node:24.22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npx ng build --configuration production

FROM nginxinc/nginx-unprivileged:1.30-alpine
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist/plantops-web/browser /usr/share/nginx/html
EXPOSE 8080

# Runtime settings, read when the container starts (see nginx/default.conf.template).
ENV API_BASE_URL=http://localhost:8080 \
    SHOW_DEMO_ACCOUNTS=false
