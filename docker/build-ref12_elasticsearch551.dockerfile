# ref12/elasticsearch-5.5.1
FROM docker.elastic.co/elasticsearch/elasticsearch:5.5.1

# Disable X-Pack
RUN rm -rdfv /usr/share/elasticsearch/plugins/x-pack

# Need to set this setting since elasticsearch fails without it
ENV discovery.type single-node