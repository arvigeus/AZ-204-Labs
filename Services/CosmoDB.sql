-- composite index for more than 1 order by

SELECT
  c.name,
  c.age,
  {
    "phoneNumber": p.number,
    "phoneType": p.type
  } AS phoneInfo
FROM c
JOIN p IN c.phones
JOIN (SELECT VALUE t FROM t IN p.tags WHERE t.name IN ("winter", "fall"))
WHERE c.age > 21 AND ARRAY_CONTAINS(c.tags, 'student') AND STARTSWITH(p.number, '123')
ORDER BY c.age DESC
OFFSET 10 LIMIT 20


SELECT c.id, udf.GetMaxNutritionValue(c.nutrients) AS MaxNutritionValue FROM c


SELECT VALUE COUNT(1) FROM models