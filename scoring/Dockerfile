# Заглушка сервиса скоринга: только стандартная библиотека Python, без зависимостей и GPU.
FROM python:3.13.16-slim

WORKDIR /app
RUN useradd --create-home --uid 10001 scoring
COPY app.py ./

USER scoring
ENV PORT=8000 \
    PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1
EXPOSE 8000

HEALTHCHECK --interval=10s --timeout=3s --retries=6 --start-period=5s \
    CMD python -c "import sys, urllib.request; sys.exit(0 if urllib.request.urlopen('http://127.0.0.1:8000/health', timeout=2).status == 200 else 1)"

CMD ["python", "app.py"]
